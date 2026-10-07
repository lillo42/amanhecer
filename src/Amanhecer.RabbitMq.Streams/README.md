# Amanhecer.RabbitMq.Streams

RabbitMQ Streams transport for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET. Bind the messaging gateway to a RabbitMQ Streams broker so `Post`/`PostAsync` can publish messages to streams and consume them through a persistent consumer.

> **Broker requirements:** RabbitMQ 3.9+ with the `rabbitmq_stream` plugin enabled. The default port is `5552`. For classic queues over AMQP 0-9-1, use the `Amanhecer.RabbitMq` transport instead.

## Configuring the gateway

Configure the connection, publications and subscriptions by adding a `RabbitMqStreamGateway` instance directly:

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Amanhecer.RabbitMq.Streams;
using Amanhecer.RabbitMq.Streams.Provisioners;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.AddGateway(new RabbitMqStreamGateway
    {
        UserName = "guest",
        Password = "guest",
        Publications =
        {
            new RabbitMqStreamPublication("greetings")
            {
                RoutingKey = "greeting",       // the key PostAsync resolves the publication with
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
    })));
```

Advanced client options can be set via the `Configuration` callback on the gateway:

```csharp
new RabbitMqStreamGateway
{
    Configuration = config => config.Heartbeat = TimeSpan.FromSeconds(60)
}
```

Per-producer and per-consumer options are available through `Configure` on the publication and `Configuration` on the subscription respectively:

```csharp
new RabbitMqStreamPublication("greetings")
{
    Configure = config => config.MaxInFlight = 1000
}

new RabbitMqStreamSubscription("greeting", "greetings")
{
    Configuration = config => config.OffsetSpec = new OffsetTypeFirst()
}
```

## Publishing

`Message.Payload` is sent as the AMQP message body. The standard AMQP 1.0 properties (`MessageId`, `CorrelationId`, `ContentType`, `ContentEncoding`, `Subject`, `ReplyTo`, `GroupId`) are mapped from the corresponding `Message` fields. In CloudEvents binary content mode (the default) the event attributes are carried as `cloudEvents:*` application properties.

The expiration applied to the message (the AMQP `AbsoluteExpiryTime` property) can be set through metadata — a `DateTime`, a `DateTimeOffset`, or a `TimeSpan` from now. The value on `Message.Metadata` wins (for example one carried over from a consumed message); the pipeline context metadata acts as the publish-time fallback:

```csharp
context.Metadata[Metadata.Expiration] = TimeSpan.FromMinutes(5);
```

The AMQP-only properties `GroupSequence`, `To` and `ReplyToGroupId` can be set through the `Metadata.GroupSequence`, `Metadata.To` and `Metadata.ReplyToGroupId` keys on `Message.Metadata`.

`message.Enrich(activity)` is called before publishing so the current trace context is propagated to consumers.

## Consuming

RabbitMQ Streams uses offset-based acknowledgement: settling a message stores the consumer offset. The offset is tracked under the subscription name (the `ConsumerConfig.Reference` defaults to it; override it through the subscription's `Configuration` callback). The transport buffers incoming messages in an in-memory channel and delivers them in batches up to `BufferSize`.

Consumers start from new messages by default (`OffsetTypeNext`); set `OffsetSpec` through the subscription's `Configuration` callback to replay from an earlier offset (for example `OffsetTypeFirst`).

Nack and defer are not natively supported by the Streams protocol — both fall back to acknowledging the message and emitting a warning log. Processing is at-least-once.

The gateway owns the producers and consumers it creates: disposing the gateway (`IAsyncDisposable`) closes them and the underlying `StreamSystem`.

## Stream provisioning

Each publication and subscription can carry a provisioner that runs when the gateway provisions its resources (`ProvisionerAsync`, called at startup by the hosted service):

- `AssumeStreamExists` — no provisioning, assume the stream already exists.
- `ValidateStreamExists` — fail fast when the stream is missing on the broker.
- `CreateStream` — create the stream when missing, with optional `MaxAge`, `MaxLengthBytes`, `MaxSegmentSizeBytes`, and `LeaderLocator`. A stream that already exists is left untouched.

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/blob/main/docs/rabbitmq-streams.md).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
