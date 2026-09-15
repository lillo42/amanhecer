# Kafka

Two packages bind the messaging gateway to an Apache Kafka cluster:

- `Amanhecer.ConfluentKafka` — built on [Confluent.Kafka](https://github.com/confluentinc/confluent-kafka-dotnet) (librdkafka).
- `Amanhecer.Dekaf` — built on [Dekaf](https://github.com/thomhurst/Dekaf), a pure C# client with no native dependencies.

> **Broker requirements:** Dekaf's consumer uses the KIP-848 consumer group protocol, so the
> Dekaf transport requires a Kafka 4.0+ broker — it cannot consume from Redpanda (which does
> not implement KIP-848). The Confluent.Kafka transport works with any Kafka-compatible
> broker, Redpanda included. For local development, `docker-compose-kafka.yaml` starts an
> Apache Kafka 4 broker (port `9092`).

Both expose the same model: publications produce messages to topics, subscriptions consume
them through a consumer group. The examples below use `Amanhecer.ConfluentKafka`; the
`Amanhecer.Dekaf` API is identical in shape — swap `ConfluentKafkaGateway` /
`ConfluentKafkaPublication` / `ConfluentKafkaSubscription` for `DekafGateway` /
`DekafPublication` / `DekafSubscription`.

## Configuring the gateway

Configure the connection, publications and subscriptions through `UsingConfluentKafka` (or
`UsingDekaf` for the Dekaf transport):

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingConfluentKafka(kafka => kafka
        .BootstrapServers("localhost:9092")
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey("greeting")        // the key PostAsync resolves the publication with
            .Topic("greetings")            // defaults to the routing key when not set
            .CreateIfNotExists(create => create.NumPartitions(6))))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey("greeting")      // consumed messages are dispatched with this routing key
            .Topic("greetings")            // defaults to the routing key when not set
            .GroupId("greeting-consumers") // defaults to the routing key when not set
            .CreateIfNotExists())))));
```

The subscription's `ToRoutingKey` must match the routing key of the handler pipeline that
processes the message (the type's full name, or its `[RoutingKey]` value). Every consumer of
the subscription joins its consumer group, so `NumberOfConsumers` consumers share the topic
partitions between them.

The client configuration can be customized per gateway before each client is created:

```csharp
.UsingConfluentKafka(kafka => kafka
    .BootstrapServers("localhost:9092")
    .ConfigureProducer(config => config.Acks = Acks.All)
    .ConfigureConsumer(config => config.SessionTimeoutMs = 30_000)
    .ConfigureAdmin(config => config.SocketTimeoutMs = 60_000))
```

(`Amanhecer.Dekaf` exposes the same `ConfigureProducer` / `ConfigureConsumer` /
`ConfigureAdmin` callbacks over Dekaf's fluent builders instead.)

## Publishing

Post a request; it is mapped to a `Message` (JSON by default), run through the publication's
encode transformers and produced to the topic:

```csharp
var dispatcher = provider.GetRequiredService<IDispatcher>();
await dispatcher.PostAsync(new Greeting("world"));
```

The `Message.PartitionKey` becomes the Kafka record key, the payload the record value, and
the message headers the record headers. In CloudEvents binary content mode (the default) the
event attributes are carried as `ce_*` record headers, following the CloudEvents Kafka
binding.

### Delivery confirmation

Publishing is fire and forget by default: the message is queued to the producer buffer and
`PostAsync` completes without delivery guarantees. Set `WaitForConfirmation()` on the
publication to await the broker's delivery report instead — broker errors then surface as
publish exceptions, and the publish only completes once the record is acknowledged:

```csharp
.Publications(publications => publications.AddPublication(publication => publication
    .RoutingKey("greeting")
    .Topic("greetings")
    .WaitForConfirmation()))
```

## Consuming

The `Amanhecer.Extensions.Hosting` package runs a consumer for every subscription together
with the host:

```csharp
using Amanhecer.Extensions.Hosting.Extensions;

builder.Services.AddAmanhecerHost();
```

Each consumed message is decoded and dispatched through the pipeline of the subscription's
routing key, then settled through the subscription's `OnError` (ack, nack, defer or move to a
dead-letter/invalid-message destination).

### Settlement and offset commits

Kafka has no per-message acknowledgement: settling a message moves the consumer group's
offset. The transports follow the store-then-commit strategy:

- **Ack** *stores* the offset following the message's record. Stored offsets are *committed*
  when the batch reaches `CommitBatchSize` (default `10`), or by a sweeper every
  `SweepUncommittedOffsetsInterval` (default 30 seconds), whichever comes first — the sweeper
  prevents low-traffic topics holding uncommitted offsets for long periods. Remaining offsets
  are committed when the consumer shuts down, and (Confluent.Kafka transport) when partitions
  are revoked during a rebalance. If the consumer stops before a flush, at most one batch of
  messages is redelivered — processing is at-least-once.
- **Nack** stores the offset the same way: the record is not consumed again. Kafka has no
  dead-letter destination of its own, so a nacked message is effectively dropped by this
  consumer group — route it to a dead-letter topic through the subscription's
  `DeadLetterQueueRoutingKey` if you need one.
- **Defer** seeks the consumer back to the message's record, so it is consumed again. When a
  delay is requested, the partition is paused for the delay before consumption resumes.
  Seeking also redelivers the records *after* the deferred one in the same partition.

Because committing or seeking an offset settles a whole partition prefix, settling messages
out of order within a partition also settles the records between them.

When a consumer group has no committed offset, consumption starts at
`AutoOffsetReset` (default `Earliest`).

## Provisioning

Publications and subscriptions declare how their topic is set up:

- `AssumeExists()` — no provisioning.
- `ValidateIfExists()` — fail fast when the topic is missing on the cluster.
- `CreateIfNotExists(...)` — create the topic when missing; an existing topic is left
  untouched:

```csharp
.Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
    .ToRoutingKey("greeting")
    .Topic("greetings")
    .GroupId("greeting-consumers")
    .CreateIfNotExists(create => create
        .NumPartitions(6)
        .ReplicationFactor(3)
        .Config("cleanup.policy", "compact"))))
```

The same provisioner behavior backs both sides, so a publication and the subscriptions of
its topic can each pick their own strategy.

Provisioning never runs while services are registered: each gateway provisions once, the
first time it is used — when the host starts its consumers, or on the first publish in
publish-only applications. A failed provisioning attempt is retried on the next use.

## Message mappers and transformers

Publications and subscriptions without an explicit message mapper fall back to the JSON
mapper. Encode/decode transformers wrap the mapping — see [Transformers](transformers.md).
