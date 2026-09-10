# Code review — `add.support.rmq` branch (vs `main`)

Temporary working document. Findings from reviewing `src/Amanhecer.RabbitMq/`, the messaging
abstraction layer (`src/Amanhecer.Abstractions/Messaging/`, `src/Amanhecer/Messaging/`,
`src/Amanhecer.Extensions.Hosting/`) and the full `main...HEAD` diff.

**Verdict:** the transport core (poller, channel locking, poison-message nack, provisioners,
backpressure) is solid, but the wiring around it had bugs that made the branch non-functional
end-to-end. The critical wiring bugs found during the review are **fixed** (see below); the
remaining items are behavior/design decisions to make before merge.

---

## Fixed during this review

- **Consume path broken end-to-end (critical).** `JsonMessageMapper.ToRequestAsync` requires
  `MetadataName.RequestType`, which nothing in production code ever set (only tests did) — every
  consumed message with the default mapper threw, hit `OnError` → `Defer` → requeue loop.
  *Fix:* `DecodeMiddleware` now derives the request type from the pipeline's `HandleTypeMetadata`
  (`src/Amanhecer/Messaging/Middlewares/DecodeMiddleware.cs:80`);
  `HandleTypeMetadata.HandlerType` is trim-annotated
  (`src/Amanhecer.Abstractions/Metadatas/HandleTypeMetadata.cs`).
- **`EncodeMiddleware`/`DecodeMiddleware` missing from DI (critical).** The dispatcher and the
  pump insert them into pipelines, but middleware is resolved via `GetRequiredService`, so every
  `PostAsync` and every consumed message threw `InvalidOperationException`.
  *Fix:* registered in `AddAmanhecer` (`src/Amanhecer/Extensions/ServiceCollectionExtensions.cs`).
- **Publication exchanges never provisioned (critical).** `RabbitMqConfigurator.CreateGateway`
  never set `RabbitMqGateway.Exchange`, and only *queue* provisioners ran exchange provisioners —
  a publish-only app published to an undeclared exchange and got its channel closed (404).
  *Fix:* `RabbitMqGateway.ProvisionerAsync` provisions every distinct publication exchange
  (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs`).
- **Publication defaults were dead config.** `EncodeMiddleware` stored the publication keyed by
  runtime type, while `SetCloudEventTransformer` reads `MetadataName.Publication` — so
  `DefaultSource`/`DefaultType`/`DefaultHeaders`/etc. were never applied.
  *Fix:* store under `MetadataName.Publication`
  (`src/Amanhecer/Messaging/Middlewares/EncodeMiddleware.cs`).
- **Gateways never disposed on shutdown.** `ConsumerHostedService.StopAsync`'s docstring claimed
  disposal but only cancelled tokens; the connection stayed open and buffered messages were never
  settled. *Fix:* `StopAsync` now disposes `IAsyncDisposable`/`IDisposable` gateways
  (`src/Amanhecer.Extensions.Hosting/ConsumerHostedService.cs`).
- **Zero-value validation gaps.** `BufferSize(0)` crashed later in `BoundedChannelOptions(0)` and
  `NumberOfConsumers(0)` silently consumed nothing; both now require `> 0`
  (`src/Amanhecer.RabbitMq/Configurations/RabbitMqSubscriptionConfigurator.cs`).
- **No disposed guard.** `GetOrCreateAsync` after `DisposeAsync` resurrected the connection or
  threw from a disposed lock; now throws `ObjectDisposedException`
  (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs`).
- **Missing package metadata.** `Amanhecer.RabbitMq.csproj` now has `Description`/`PackageTags`
  like its siblings.

## Critical — decide before merge

1. **The defer story is dangerous.** `Subscription.OnError` defaults to `Defer(5s)` for any
   non-`InvalidMessageException` (`src/Amanhecer.Abstractions/Messaging/Subscription.cs:76`), but
   `RabbitMqConsumer.DeferAsync` requeues *immediately*
   (`src/Amanhecer.RabbitMq/RabbitMqConsumer.cs:56`). A persistently failing handler spins a hot
   receive → fail → requeue loop with no backoff and no redelivery cap, hammering broker and CPU.
   *Options:* real delayed retry (per-message TTL + DLX, or the delayed-message plugin), or check
   the `Redelivered` flag / `x-death` count and escalate to nack after N attempts. Don't ship
   immediate-requeue behind a 5-second-delay API.

## Should fix

2. **`CloudEventType.Json` is a no-op.** Selecting structured mode returns early from
   `SetCloudEventHeaders` (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:143`) and
   `JsonMessageMapper` serializes the raw request, not a CloudEvents envelope. Implement
   structured mode or throw `NotSupportedException` until implemented.
3. **`AdditionalCloudEvents` is accepted but never applied.** Collected by the configurator and
   stored on `Publication.AdditionalCloudEvents`, read by no one. Emit them as
   `cloudEvents:{key}` headers in `SetCloudEventHeaders`, or remove the API.
4. **`Mandatory` publishes can silently lose messages.** With `mandatory: true` the broker returns
   unroutable messages via basic.return, but no return handler is registered and publisher
   confirms are never enabled — publish "success" (and the `amanhecer.message.publish.success`
   counter) is recorded for messages that reached no queue
   (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:97`). At minimum hook `BasicReturnAsync` and
   log/count returns; ideally offer publisher confirms via `ChannelOptions` and document the
   semantics.
5. **Prefetch/buffer mismatch.** QoS prefetch is `BufferSize * NumberOfConsumers`
   (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs`), but the bounded buffer holds only `BufferSize`
   and each pump reads one message per `GetMessagesAsync`. Once the buffer is full,
   `HandleBasicDeliver` blocks in `WriteAsync`, stalling the channel; extra prefetch just
   accumulates unacked messages. Prefetch should be roughly `BufferSize + NumberOfConsumers`, or
   buffer capacity should scale with `NumberOfConsumers`.
6. **Sync-over-async network I/O during DI registration.** `CreateProducers()`/`CreateConsumer()`
   block on `.GetAwaiter().GetResult()` (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs`) and
   `AddGateway` blocks on `ProvisionerAsync()`
   (`src/Amanhecer/Configurator/AmanhecerMessagingConfigurator.cs:168`) — `AddAmanhecer` opens a
   broker connection synchronously while building the service collection: deadlock hazard under a
   `SynchronizationContext`, startup-blocking, no retry. Prefer deferring connection creation to
   `ConsumerHostedService.StartAsync`, or at least document fail-fast-at-registration.
7. **Null-forgiving `Exchange!` in the producer.** `rabbitMqPublication.Exchange!.Name` is
   dereferenced before the try block (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:68`) — a
   hand-built `RabbitMqPublication` without an exchange throws a bare NRE with no failure metric.
   Validate and throw `InvalidOperationException` with a clear message.
8. **Culture-sensitive time round-trip.** `message.Time.ToString("O")`
   (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:155`) and `DateTimeOffset.TryParse`
   (`src/Amanhecer.RabbitMq/RabbitMqMessagePoller.cs:292`) use the ambient culture; pass
   `CultureInfo.InvariantCulture` on both sides.
9. **Metadata keying is internally inconsistent** (runtime-type keys in `SetMetadata(object)` vs
   `typeof(T).FullName` in `GetMetadata<T>()` vs constant string keys). Root cause of the
   publication-defaults bug above; will bite again. Unify the scheme.
10. **`PostCoreAsync` mutates the caller's context in place** (overwrites `RoutingKey`, stores the
    original in metadata, `src/Amanhecer/AmanhecerDispatcher.cs:683`). Reusing one
    `AmanhecerContext` for two `PostAsync` calls corrupts `PublicationRoutingKey`. Clone
    internally before mutating, like the strategies do.

## Branch-wide concerns

- **Breaking API sweep** (defensible for `1.0.1-alpha`, but document it): `IContext`,
  `IPipelineContext`, `IPipelineContextFactory` and `IMiddleware.Initialize` are deleted;
  `IDispatcher`, `IMiddleware`, `IHandlerFactory`, `IMiddlewareFactory`, `IPipeline`,
  `IPipelineFactory`, `IExecutingStrategy` moved to `AmanhecerContext`/`IReadOnlyList`;
  `ResiliencePipelineMiddleware.ResilienceContextKey` renamed to `ResilienceContext`. Needs
  migration notes.
- **`TreatWarningsAsErrors` flipped from `true` to `false`** (`Directory.Build.props:19`). If that
  silenced warnings from this branch, fix the warnings instead — quiet quality regression for the
  whole repo.
- `AmanhecerPipelineFactory` builds middleware against a throwaway `AmanhecerContext` "metadata
  bag" merged at execution — works, but deserves a real type instead of a repurposed context.
- The dispatcher rewrite is otherwise behavior-preserving (send/publish/query semantics, telemetry
  tags, per-pipeline cloning all match the old code). The RabbitMQ CI job is good.

## Nits

- `CreateIfNotExchange` reads like a typo for `CreateExchangeIfNotExists`; `ProvisionerAsync` →
  `ProvisionAsync`; `ValidateIfExists` is a property-ish name for an action.
- Two `MetadataName` classes (`Amanhecer.Abstractions.MetadataName`,
  `Amanhecer.RabbitMq.MetadataName`) with overlapping concepts — one typo'd `using` away from a
  bug. Consider `RabbitMqMetadataName`.
- `PrefetchSize` is exposed even though its own doc says RabbitMQ ignores it
  (`src/Amanhecer.RabbitMq/RabbitMqSubscription.cs:28`) — API noise.
- `RabbitMqPublicationConfigurator.RabbitMqRoutingKey` accepts empty strings while `RoutingKey`
  rejects them — inconsistent; if empty is intentionally valid AMQP, document it.
- Tests target only `net10.0`; the entire net462 path (RabbitMQ.Client 6.8.1, `IModel`, sync
  dispose) is compile-only and has never run against a broker.
