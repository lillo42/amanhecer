# RabbitMQ Streams

The `Amanhecer.RabbitMq.Streams` package binds the messaging gateway to a RabbitMQ broker's
streams: publications produce messages to streams, subscriptions consume them with offset-based
acknowledgement. It requires RabbitMQ 3.9+ with the `rabbitmq_stream` plugin enabled (default
port `5552`). For classic queues over AMQP 0-9-1, use the [`Amanhecer.RabbitMq`](rabbitmq.md)
transport instead.

A broker with the stream plugin enabled is available via the repository's compose file:

```bash
docker compose -f docker-compose-rabbitmq.yaml up -d
```

## Configuring the gateway

Configure the connection, publications and subscriptions through `UsingRabbitMqStreams`:

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingRabbitMqStreams(streams => streams
        .Connection(connection => connection
            .Credentials("guest", "guest")
            .Endpoint("localhost", 5552))
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey("greeting")      // the key PostAsync resolves the publication with
            .Stream("greetings")
            .CreateIfNotExists()))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey("greeting")    // consumed messages are dispatched with this routing key
            .Stream("greetings")
            .CreateIfNotExists())))));
```

Alternatively, add a `RabbitMqStreamGateway` instance directly:

```csharp
messaging.AddGateway(new RabbitMqStreamGateway
{
    UserName = "guest",
    Password = "guest",
    Publications =
    {
        new RabbitMqStreamPublication("greetings")
        {
            RoutingKey = "greeting",
            Provisioner = new CreateStream()
        }
    },
    Subscriptions =
    {
        new RabbitMqStreamSubscription("greeting", "greetings")
        {
            Provisioner = new CreateStream()
        }
    }
});
```

Advanced client options can be set through the `Configuration` callback on the gateway
(a `StreamSystemConfig`), `Configure` on the publication (a `ProducerConfig`) and
`Configuration` on the subscription (a `ConsumerConfig`):

```csharp
new RabbitMqStreamGateway
{
    Configuration = config => config.Heartbeat = TimeSpan.FromSeconds(60)
}

new RabbitMqStreamPublication("greetings")
{
    Configure = config => config.MaxInFlight = 1000
}

new RabbitMqStreamSubscription("greeting", "greetings")
{
    Configuration = config => config.OffsetSpec = new OffsetTypeFirst()
}
```

The gateway owns the producers and consumers it creates: disposing the gateway
(`IAsyncDisposable`) closes them and the underlying `StreamSystem`.

## Publishing

`Message.Payload` is sent as the AMQP message body. The standard AMQP 1.0 properties
(`MessageId`, `CorrelationId`, `ContentType`, `ContentEncoding`, `Subject`, `ReplyTo`,
`GroupId`) are mapped from the corresponding `Message` fields. In CloudEvents binary content
mode (the default) the event attributes are carried as `cloudEvents:*` application properties.

`message.Enrich(activity)` is called before publishing so the current trace context is
propagated to consumers.

The expiration applied to the message (the AMQP `AbsoluteExpiryTime` property) can be set
through metadata — a `DateTime`, a `DateTimeOffset`, or a `TimeSpan` from now. The value on
`Message.Metadata` (for example one carried over from a consumed message) wins; the pipeline
context metadata acts as the publish-time fallback:

```csharp
context.Metadata[Metadata.Expiration] = TimeSpan.FromMinutes(5);
await producer.ProduceAsync(message, publication, context);
```

The AMQP-only properties `GroupSequence`, `To` and `ReplyToGroupId` can be set the same way,
through the `Metadata.GroupSequence`, `Metadata.To` and `Metadata.ReplyToGroupId` keys on
`Message.Metadata`.

## Consuming

RabbitMQ Streams uses offset-based acknowledgement: settling a message stores the consumer
offset. The offset is tracked under the subscription name — the `ConsumerConfig.Reference`
defaults to it and can be overridden through the subscription's `Configuration` callback. The
transport buffers incoming messages in an in-memory channel and delivers them in batches up
to `BufferSize`.

Consumers start from new messages by default (`OffsetTypeNext`); set `OffsetSpec` through the
subscription's `Configuration` callback to replay from an earlier offset (for example
`OffsetTypeFirst`).

Nack and defer are not natively supported by the Streams protocol — both fall back to
acknowledging the message and emitting a warning log. Processing is at-least-once.

## Provisioning

Each publication and subscription can carry a provisioner that runs when the gateway
provisions its resources (`ProvisionerAsync`, called at startup by the hosted service):

- `AssumeStreamExists` — no provisioning, assume the stream already exists.
- `ValidateStreamExists` — fail fast when the stream is missing on the broker.
- `CreateStream` — create the stream when missing, with optional `MaxAge`, `MaxLengthBytes`,
  `MaxSegmentSizeBytes`, and `LeaderLocator`. A stream that already exists is left untouched.

## Message mappers and transformers

Publications and subscriptions use the same message mappers and decode transformers as every
other transport; see [Transformers](transformers.md).
