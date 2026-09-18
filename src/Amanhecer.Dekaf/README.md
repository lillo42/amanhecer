# Amanhecer.Dekaf

Kafka transport for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET, built on [Dekaf](https://github.com/thomhurst/Dekaf), a pure C# Kafka client with no native dependencies. Bind the messaging gateway to an Apache Kafka cluster, so `Post`/`PostAsync` can publish messages to topics and consume them through a consumer group.

> **Broker requirements:** Dekaf's consumer uses the KIP-848 consumer group protocol, so this transport requires a Kafka 4.0+ broker — it cannot consume from Redpanda (which does not implement KIP-848). For other Kafka-compatible brokers, use the `Amanhecer.ConfluentKafka` transport. For local development, the repository's `docker-compose-kafka.yaml` starts an Apache Kafka 4 broker (port `9092`).

## Configuring the gateway

Configure the connection, publications and subscriptions through `UsingDekaf`:

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingDekaf(kafka => kafka
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

The client configuration can be customized before each client is created, through `ConfigureProducer` / `ConfigureConsumer` / `ConfigureAdmin` callbacks over Dekaf's fluent builders.

## Publishing

The `Message.PartitionKey` becomes the Kafka record key, the payload the record value, and the message headers the record headers. In CloudEvents binary content mode (the default) the event attributes are carried as `ce_*` record headers.

Publishing is fire and forget by default. Set `WaitForConfirmation()` on the publication to await the broker's delivery report instead — broker errors then surface as publish exceptions.

## Consuming

Kafka has no per-message acknowledgement: settling a message moves the consumer group's offset. The transport follows the store-then-commit strategy — acked and nacked offsets are committed in batches (`CommitBatchSize`, default `10`) or by a sweeper (`SweepUncommittedOffsetsInterval`, default 30 seconds), and deferred messages seek the consumer back to the record. Processing is at-least-once. When a group has no committed offset, consumption starts at `AutoOffsetReset` (default `Earliest`).

## Topic provisioning

- `AssumeExists()` — no provisioning.
- `ValidateIfExists()` — fail fast when the topic is missing on the cluster.
- `CreateIfNotExists(...)` — create the topic when missing, with `NumPartitions(...)`, `ReplicationFactor(...)` and `Config(...)`.

Provisioning never runs while services are registered: each gateway provisions once, the first time it is used, and a failed attempt is retried on the next use.

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/blob/main/docs/kafka.md).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
