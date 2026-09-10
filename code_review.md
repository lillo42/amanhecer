# Code review — `add.support.rmq` branch (vs `main`)

Temporary working document. Findings from reviewing `src/Amanhecer.RabbitMq/`, the messaging
abstraction layer (`src/Amanhecer.Abstractions/Messaging/`, `src/Amanhecer/Messaging/`,
`src/Amanhecer.Extensions.Hosting/`) and the full `main...HEAD` diff.

**Verdict:** the transport core (poller, channel locking, poison-message nack, provisioners) is
solid. The critical wiring bugs found in the first pass are **fixed** (see below). But a second
pass over the dispatcher/pipeline rewrite found that it is **not** behaviour-preserving: items
1–4 are regressions against `main` that no test covers, and item 1 silently discards messages.
Those, plus the defer hot loop (item 5), should block the merge.

**Verification state:** `dotnet build Amanhecer.slnx -t:Rebuild` is clean across all TFMs. All
unit and integration tests pass (388 total: 204 `Amanhecer.Tests`, 98 `Abstractions`, 33
`IntegrationTests`, 17 `Extensions.Hosting`, 14 `Extensions.Resilience`, 12 `Polly`, 10
`OpenTelemetry`). `Amanhecer.RabbitMq.Tests` is 32/46 without a broker — the 14 failures are all
`ConnectFailureException` to `127.0.0.1:5672` and run green in the dedicated CI job.
`Messaging.Base.Tests` is a contract library with no runnable tests. **None of the findings below
are caught by the test suite.**

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
  (`src/Amanhecer.Extensions.Hosting/ConsumerHostedService.cs`). **See item 6** — this fix
  interacts badly with gateways being registered as singletons.
- **Zero-value validation gaps.** `BufferSize(0)` crashed later in `BoundedChannelOptions(0)` and
  `NumberOfConsumers(0)` silently consumed nothing; both now require `> 0`
  (`src/Amanhecer.RabbitMq/Configurations/RabbitMqSubscriptionConfigurator.cs`).
- **No disposed guard.** `GetOrCreateAsync` after `DisposeAsync` resurrected the connection or
  threw from a disposed lock; now throws `ObjectDisposedException`
  (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs`).
- **Missing package metadata.** `Amanhecer.RabbitMq.csproj` now has `Description`/`PackageTags`
  like its siblings.

## Critical — decide before merge

1. **Unrouted messages are silently acked and discarded.** New in this branch: when a routing key
   has no configured pipeline *but* `context.Middlewares != null`, `AmanhecerPipelineFactory`
   fabricates an empty pipeline (`src/Amanhecer/AmanhecerPipelineFactory.cs:29`), so
   `pipelines.Count == 1` and the dispatcher's `case 0: throw PipelineNotFoundException` guard
   never fires. `AmanhecerMessagePump.ProcessAsync` *always* sets
   `Middlewares = [DecodeMiddleware]` (`src/Amanhecer/Messaging/AmanhecerMessagePump.cs:200`), so
   a subscription whose `ToRoutingKey` has no registered handler decodes the message, runs
   nothing, returns a null `Response` → `Ack` → **message gone**. On `main` the same path returned
   empty and threw loudly. The fabrication is not load-bearing for the `PostAsync` path:
   `"Amanhecer.Messaging.Post"` is explicitly registered
   (`src/Amanhecer/Extensions/ServiceCollectionExtensions.cs:76`). Verified: `SendAsync(new
   object(), ctx)` throws `PipelineNotFoundException` without context middlewares and silently
   succeeds with them. *Fix:* drop the fabricated pipeline, or gate it on an explicit opt-in.
2. **Per-middleware metadata collides — all instances of a type see the last registration.** All
   middlewares of a pipeline are created against one shared `metadataBag`
   (`src/Amanhecer/AmanhecerPipelineFactory.cs:48`), and `SetMetadata(metadata)` keys by the
   metadata's runtime type (`src/Amanhecer/AmanhecerMiddlewareFactory.cs:25`). Verified:
   `Use<TagMiddleware>(1, new Tag("first")).Use<TagMiddleware>(2, new Tag("second"))` makes *both*
   instances read `Tag { Name = "second" }`. This is a regression from deleting
   `IMiddleware.Initialize(metadata)`, and it breaks the new resilience middleware directly: a
   `[ResiliencePipeline("fast")]` on the handler class plus `[ResiliencePipeline("slow")]` on
   `HandleAsync` makes both middlewares execute `"slow"`. *Fix:* key the metadata per middleware
   instance (index/order), or restore an explicit per-instance hand-off.
3. **Retry/hedging attempts are no longer isolated.** `main` passed
   `context.DeepClone(cancellationToken)` to `next`; the branch mutates and reuses the *same*
   context across attempts
   (`src/Amanhecer.Extensions.Resilience/ResiliencePipelineMiddleware.cs:119`, same code at
   `src/Amanhecer.Polly/PollyResiliencePipelineMiddleware.cs:123`). Concretely: a
   failed attempt leaves `context.Request` replaced by a `Message` (set downstream by
   `EncodeMiddleware`), so the retry short-circuits at `if (context.Request is Message)` and skips
   mapping and transformers entirely. With a Polly *hedging* strategy the concurrent attempts also
   race on the shared `Metadata` dictionary and on the `CancellationToken` save/restore.
   *Fix:* restore per-attempt cloning.
4. **`AddAmanhecer` throws at startup on `net462`/`netstandard2.0`.**
   `foreach (var pipelineName in transformerPipelines.Keys)` assigns
   `transformerPipelines[pipelineName] = …` inside the loop
   (`src/Amanhecer/Extensions/ServiceCollectionExtensions.cs:126`). Modern .NET tolerates
   overwriting an existing key during enumeration; .NET Framework's `Dictionary.Insert` bumps
   `version` on overwrite, so the next `MoveNext` throws
   `InvalidOperationException: Collection was modified`. Triggers whenever at least one global
   transformer and two or more named transformer pipelines are configured. *Fix:* snapshot the
   keys with `ToList()` first. See also the CI gap under *Branch-wide concerns* — this is exactly
   the class of bug that gap hides.
5. **The defer story is dangerous.** `Subscription.OnError` defaults to `Defer(5s)` for any
   non-`InvalidMessageException` (`src/Amanhecer.Abstractions/Messaging/Subscription.cs:76`), but
   `RabbitMqConsumer.DeferAsync` requeues *immediately*
   (`src/Amanhecer.RabbitMq/RabbitMqConsumer.cs:56`). A persistently failing handler spins a hot
   receive → fail → requeue loop with no backoff and no redelivery cap, hammering broker and CPU.
   `MoveToDeadLetterQueueConsumerAction` degrades to `Defer.Instance` (zero delay) when no DLQ
   routing key is set, hitting the same loop. *Options:* real delayed retry (per-message TTL +
   DLX, or the delayed-message plugin), or check the `Redelivered` flag / `x-death` count and
   escalate to nack after N attempts. Don't ship immediate-requeue behind a 5-second-delay API.
   (The `DeferAsync` docstring now admits the delay is ignored, but the behaviour is unchanged.)

## Should fix

6. **A stop/start cycle uses disposed gateways.** `ConsumerHostedService.StopAsync` disposes every
   resolved `IGateway` (`src/Amanhecer.Extensions.Hosting/ConsumerHostedService.cs:82`), but
   `AddGateway` registers them via `Services.AddSingleton(gateway)` — a pre-built *instance*
   (`src/Amanhecer/Configurator/AmanhecerMessagingConfigurator.cs:189`). Since `StartAsync` calls
   `StopAsync` first (`:34`), a second `StartAsync` re-resolves the same disposed instances and
   `CreateConsumer` → `GetOrCreateAsync` throws `ObjectDisposedException`. The `StartAsync`
   docstring at `:28` claims the opposite ("the gateways are re-resolved on every start, so a
   service restarted after `StopAsync` does not reuse disposed gateway instances") — that is
   false. The `IProducerFinder` singleton also holds the now-disposed `RabbitMqProducer` channels,
   so any post after shutdown fails. *Fix:* register a gateway *factory*, or move disposal to
   container disposal and drop it from `StopAsync`.
7. **`Enrich` throws on duplicate baggage keys, killing the publish.**
   `new Baggage(activity.Baggage)`
   (`src/Amanhecer.Abstractions/Extensions/MessageExtensions.cs:38`) uses the
   `Dictionary<string, string?>(IEnumerable<KVP>)` constructor, which has `Add` semantics;
   `Activity.Baggage` enumerates the whole activity chain and can yield the same key twice
   (parent `AddBaggage("k","v1")`, child `AddBaggage("k","v2")`). Verified:
   `ArgumentException: An item with the same key has already been added. Key: k`. `Enrich` is
   called from `RabbitMqProducer.ProduceAsync` at `:92`, **outside** the try block, so the publish
   dies with an opaque `ArgumentException` and no `publish.failed` metric. The `else` branch two
   lines below already guards with `ContainsKey` — apply the same guard in the `if` branch.
8. **QoS prefetch: unchecked `(ushort)` cast, and a prefetch/buffer mismatch.**
   `(ushort)(BufferSize * NumberOfConsumers)`
   (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs:194`, and `:185` for net462) overflows silently:
   `BufferSize=256, NumberOfConsumers=256` → 65536 → wraps to **0**, which RabbitMQ interprets as
   *unlimited* prefetch, so unacked deliveries grow without bound; `1000 × 100` → 34464.
   Separately, even without overflow the arithmetic is wrong: one poller/channel is shared by
   *all* `NumberOfConsumers` consumers (`:178-208`), so the total bounded buffer is `BufferSize`
   while prefetch is `BufferSize × N`. Once the buffer is full `HandleBasicDeliverAsync` blocks in
   `WriteAsync` and stalls the channel, and the extra prefetch just accumulates unacked messages.
   *Fix:* validate the product, and make prefetch roughly `BufferSize + NumberOfConsumers` (or
   scale buffer capacity with the consumer count).
9. **`PostCoreAsync` mutates the caller's context in place.** It overwrites `context.RoutingKey`
   with `"Amanhecer.Messaging.Post"` and stores the *current* value as `PublicationRoutingKey`
   (`src/Amanhecer/AmanhecerDispatcher.cs:683`). On a second `PostAsync(msg, sameContext)`,
   `PrepareContext` sees a non-empty `RoutingKey` and skips re-resolution, so
   `PublicationRoutingKey` becomes `"Amanhecer.Messaging.Post"`. Verified: post #1 resolves
   `Amanhecer.Abstractions.Messaging.Message`; post #2 throws
   `InvalidOperationException: No publication is configured…`. Line `:691` also re-`Insert`s
   `EncodeMiddleware` into `context.Middlewares` on every call, growing the list unboundedly.
   *Fix:* clone the context before mutating, like the strategies do.
10. **A throwing error handler leaves the message unsettled.**
    `ApplyActionAsync(message, subscription.OnError(message, e), …)` runs unguarded inside
    `catch (Exception e)` (`src/Amanhecer/Messaging/AmanhecerMessagePump.cs:162`). If the action
    throws — e.g. the default `MoveToInvalidConsumerAction` reposts via `dispatcher.PostAsync` and
    `PostMessageHandler` throws `InvalidOperationException` because `InvalidMessageRoutingKey` has
    no matching publication — the exception escapes `ProcessesAsync`, so the message is never
    acked or nacked and stays unacked until the channel closes. (For RabbitMQ specifically it does
    not lose a batch: `GetMessagesAsync` returns exactly one message.) *Fix:* wrap the error-path
    settle in its own try/catch with a fallback nack.
11. **`CloudEventType.Json` is a no-op.** Selecting structured mode returns early from
    `SetCloudEventHeaders` (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:143`) and
    `JsonMessageMapper` serializes the raw request, not a CloudEvents envelope. Implement
    structured mode or throw `NotSupportedException` until implemented.
12. **`AdditionalCloudEvents` is accepted but never applied.** Written only at
    `src/Amanhecer.RabbitMq/Configurations/RabbitMqPublicationConfigurator.cs:416`, read by no
    one. Emit them as `cloudEvents:{key}` headers in `SetCloudEventHeaders`, or remove the API.
13. **`Mandatory` publishes can silently lose messages.** With `mandatory: true` the broker
    returns unroutable messages via basic.return, but no return handler is registered — publish
    "success" (and the `amanhecer.message.publish.success` counter) is recorded for messages that
    reached no
    queue (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:97`). Note the fix is cheaper than it
    looks: `RabbitMqPublication.ChannelOptions` already accepts
    `CreateChannelOptions(publisherConfirmationsEnabled: true, …)`, and in RabbitMQ.Client 7 that
    makes `BasicPublishAsync` throw on a mandatory return. So this is mostly a **default and
    docs** decision — neither `Mandatory` nor `ChannelOptions` is mentioned anywhere in
    `docs/rabbitmq.md`. At minimum document the combination; ideally hook `BasicReturnAsync` and
    log/count returns for the confirms-off path.
14. **Sync-over-async network I/O during DI registration.** `CreateProducers()`/`CreateConsumer()`
    block on `.GetAwaiter().GetResult()` (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs:145,181,191`)
    and `AddGateway` blocks on `ProvisionerAsync()`
    (`src/Amanhecer/Configurator/AmanhecerMessagingConfigurator.cs:168`) — `AddAmanhecer` opens a
    broker connection synchronously while building the service collection: deadlock hazard under a
    `SynchronizationContext`, startup-blocking, no retry. Prefer deferring connection creation to
    `ConsumerHostedService.StartAsync`, or at least document fail-fast-at-registration.
15. **Null-forgiving `Exchange!` in the producer.** `rabbitMqPublication.Exchange!.Name` is
    dereferenced before the try block (`src/Amanhecer.RabbitMq/RabbitMqProducer.cs:68`), and
    `RabbitMqPublicationsConfigurator.AddPublication(RabbitMqPublication)` accepts a hand-built
    instance that never goes through `ToPublication()`'s validation. Such a publication yields a
    bare `NullReferenceException` from the metric-tag construction, with no failure counter.
    Validate and throw `InvalidOperationException` naming the publication.
16. **Culture-sensitive time parse.** `DateTimeOffset.TryParse`
    (`src/Amanhecer.RabbitMq/RabbitMqMessagePoller.cs:292`) uses the ambient culture when reading
    `cloudEvents:time`; pass `CultureInfo.InvariantCulture`. *(Correction to the first pass: the
    publish side, `message.Time.ToString("O")` at `RabbitMqProducer.cs:155`, is **not** affected —
    the round-trip specifier always uses the invariant culture by definition. Passing
    `InvariantCulture` there is still fine for CA1305 consistency.)*
17. **Metadata keying is internally inconsistent** (runtime-type keys in `SetMetadata(object)` vs
    `typeof(T).FullName` in `GetMetadata<T>()` vs constant string keys, see
    `src/Amanhecer.Abstractions/Extensions/AmanhecerContextExtensions.cs:18-49`). Storing a
    `RabbitMqPublication` and reading `GetMetadata<IPublication>()` silently misses. Root cause of
    the publication-defaults bug above *and* of item 2; it will bite again. Unify the scheme.
18. **Fanout publications cannot be configured.** `RabbitMqRoutingKey(string)` accepts `""` with
    no validation (unlike `RoutingKey`), but `ToPublication()` rejects null *or empty*
    (`src/Amanhecer.RabbitMq/Configurations/RabbitMqPublicationConfigurator.cs:390`) — so the
    empty routing key that `fanout` exchanges conventionally use throws
    `InvalidOperationException`. Either allow empty here and document it, or reject it at the
    setter so the error surfaces at the call site. *(Promoted from a nit in the first pass: this
    is a functional gap, not just an inconsistency.)*
19. **`HandleBasicDeliverAsync` writes to the buffer with no cancellation token**
    (`src/Amanhecer.RabbitMq/RabbitMqMessagePoller.cs:102`). With a full buffer it blocks
    indefinitely, and `poller.Complete()` on dispose makes the pending `WriteAsync` throw
    `ChannelClosedException` out of the consumer callback. Pass the delivery's token and handle
    the closed-channel case.
20. **`CreateProducers` throws a bare `ArgumentException`** for two publications sharing a
    `RoutingKey` (`src/Amanhecer.RabbitMq/RabbitMqGateway.cs:158`, raw `Dictionary.Add`). Detect
    the duplicate and name it.

## Branch-wide concerns

- **The dispatcher rewrite is *not* behaviour-preserving.** *(Correction to the first pass, which
  signed this off.)* Items 1, 2 and 3 are all regressions against `main` introduced by the
  rewrite, and none of them is covered by the 388 passing tests. Send/publish/query semantics and
  telemetry tags do match the old code; pipeline construction and per-attempt cloning do not.
- **PR CI never compiles the non-`net10.0` targets.** `.github/workflows/build.yml` runs only
  `dotnet test` per test project, and every test project is `net10.0`, so `src/` compiles just its
  `net10.0` asset. The `net462`/`net8.0`/`net9.0`/`netstandard2.0` paths — including all the
  `#if NETFRAMEWORK` RabbitMQ code this branch adds — first compile in `publish.yml`, after merge.
  Item 4 is a `net462`-only startup crash that this gap hides. *Fix:* add
  `dotnet build Amanhecer.slnx` to `build.yml`. The RabbitMQ service-container job itself is good.
- **`TreatWarningsAsErrors` flipped from `true` to `false`** (`Directory.Build.props:19`).
  Measured: re-enabling it produces exactly **three** warnings, all in test projects — `IL2091` at
  `tests/Amanhecer.IntegrationTests/MiddlewareTests.cs:202` and `:203`, and `IL2067` at
  `tests/Amanhecer.Tests/AmanhecerDispatcherTests.cs:664`. Every `src/` project compiles
  warning-clean on every TFM. So the fix is small: annotate those three generics (or scope
  `false` to `tests/`) and restore `true` repo-wide. Separately, `NU1902`
  (`Microsoft.Build.Tasks.Git` 8.0.0 advisory, transitive via SourceLink) fails restore under
  `-warnaserror` and needs an explicit `NoWarn`.
- **Breaking API sweep** (defensible for `1.0.1-alpha`, but document it): `IContext`,
  `IPipelineContext`, `IPipelineContextFactory` and `IMiddleware.Initialize` are deleted;
  `IDispatcher`, `IMiddleware`, `IHandlerFactory`, `IMiddlewareFactory`, `IPipeline`,
  `IPipelineFactory`, `IExecutingStrategy` moved to `AmanhecerContext`/`IReadOnlyList`;
  `ResiliencePipelineMiddleware.ResilienceContextKey` renamed to `ResilienceContext`. Needs
  migration notes. Note that deleting `IMiddleware.Initialize` is what caused item 2.
- `AmanhecerPipelineFactory` builds middleware against a throwaway `AmanhecerContext` "metadata
  bag" merged at execution — deserves a real type instead of a repurposed context, and see item 2
  for why the shared bag is actively wrong.
- `AmanhecerRoutingConfigurator.cs:166` now passes `new HandleTypeMetadata(_handlerType!)`, so
  `AddRoutingKey("k", cfg => cfg.Use<X>())` with no `UseHandler` fails at *dispatch* time with
  `ArgumentNullException: Value cannot be null. (Parameter 'serviceType')` instead of at
  configuration time.

## Nits

- `CreateIfNotExchange` reads like a typo for `CreateExchangeIfNotExists`; `ProvisionerAsync` →
  `ProvisionAsync`; `ValidateIfExists` is a property-ish name for an action.
- Two `MetadataName` classes (`Amanhecer.Abstractions.MetadataName`,
  `Amanhecer.RabbitMq.MetadataName`) with overlapping concepts — one typo'd `using` away from a
  bug. Consider `RabbitMqMetadataName`.
- `PrefetchSize` is exposed even though its own doc says RabbitMQ ignores it
  (`src/Amanhecer.RabbitMq/RabbitMqSubscription.cs:28`) — API noise.
- `RabbitMqMessagePoller._channelLock` (a `SemaphoreSlim`) is never disposed.
- The message pump always faults on graceful shutdown and the `break` at
  `src/Amanhecer/Messaging/AmanhecerMessagePump.cs:75` is dead code: the
  `catch (OperationCanceledException)` handler awaits
  `Task.Delay(subscription.NoMessageDelay, cancellationToken)` with an already-cancelled token,
  which throws immediately. Verified with the default `NoMessageDelay` (300 ms):
  `pump.ExecuteAsync` throws `TaskCanceledException`. The pump tests only pass because they set
  `NoMessageDelay = TimeSpan.Zero`. `ConsumerHostedService` happens to swallow it, so this is
  cosmetic today — but the public `IMessagePump` contract is wrong for any other caller. Drop the
  delay in that handler.
- Tests target only `net10.0`; the entire net462 path (RabbitMQ.Client 6.8.1, `IModel`, sync
  dispose) is compile-only and has never run against a broker — and per the CI note above, it is
  not even compiled on PRs.
- This file is a temporary working document and should not ship in the branch.
