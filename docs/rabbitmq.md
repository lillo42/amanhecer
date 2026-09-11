# RabbitMQ

The `Amanhecer.RabbitMq` package binds the messaging gateway to a RabbitMQ broker: publications
produce messages to exchanges, subscriptions consume them from queues.

A broker for local development is available via the repository's compose file:

```bash
docker compose -f docker-compose-rabbitmq.yaml up -d
```

## Configuring the gateway

Configure the connection, publications and subscriptions through `UsingRabbitMQ`:

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingRabbitMQ(rabbitMq => rabbitMq
        .Connection(connection => connection
            .Uri(new Uri("amqp://guest:guest@localhost:5672")))
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey("greeting")          // the key PostAsync resolves the publication with
            .RabbitMqRoutingKey("greeting")  // the key the message is published to the exchange with
            .Persistent()
            .Exchange(exchange => exchange
                .Name("greetings")
                .CreateIfNotExists(create => create.Type("topic").Durable(true)))))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey("greeting")        // consumed messages are dispatched with this routing key
            .QueueName("greetings")
            .CreateIfNotExists(create => create
                .Exchange(exchange => exchange
                    .Name("greetings")
                    .CreateIfNotExists(exchangeCreate => exchangeCreate.Type("topic").Durable(true)))
                .RoutingKey("greeting")
                .Durable(true)))))));
```

The subscription's `ToRoutingKey` must match the routing key of the handler pipeline that
processes the message (the type's full name, or its `[RoutingKey]` value).

## Publishing

Post a request; it is mapped to a `Message` (JSON by default), run through the publication's
encode transformers and produced to the exchange:

```csharp
var dispatcher = provider.GetRequiredService<IDispatcher>();
await dispatcher.PostAsync(new Greeting("world"));
```

### Mandatory publishes and publisher confirmations

`Mandatory()` publishes messages with the AMQP `mandatory` flag, so the broker returns
messages that cannot be routed to any queue instead of silently dropping them. Returned
messages are logged as warnings and counted through the
`amanhecer.message.publish.returned` metric, but the publish itself still completes
successfully: basic.return is asynchronous.

To surface unroutable messages as publish exceptions, combine `Mandatory()` with publisher
confirmations on the channel the publication publishes through — with confirmations enabled,
`BasicPublishAsync` throws when a mandatory message is returned. Confirmations are enabled
through `RabbitMqPublication.ChannelOptions` (or gateway-wide through
`RabbitMqGateway.ChannelOptions`):

```csharp
var publication = new RabbitMqPublication
{
    RoutingKey = "greeting",
    RabbitMqRoutingKey = "greeting",
    Mandatory = true,
    Exchange = new Exchange { Name = "greetings" },
    ChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true)
};
```

`ChannelOptions` is available on the modern RabbitMQ.Client 7 targets; the .NET Framework
target (RabbitMQ.Client 6) does not support it, and returned messages are only logged and
counted there.

### Fanout exchanges

Publications to fanout exchanges conventionally publish with an empty routing key. This is
allowed only when the exchange is declared as fanout, so an empty key cannot be set by
mistake on exchange types that would silently not route:

```csharp
.Publications(publications => publications.AddPublication(publication => publication
    .RoutingKey("greeting")      // still required: resolves the publication on PostAsync
    .RabbitMqRoutingKey("")      // fanout exchanges ignore the routing key
    .Exchange(exchange => exchange
        .Name("greetings")
        .CreateIfNotExists(create => create.Type("fanout")))))
```

### CloudEvents content modes

Messages are published in CloudEvents binary content mode by default: the event attributes
are carried as `cloudEvents:*` AMQP headers and the body holds the payload. Structured
(JSON) content mode is also supported:

```csharp
.Publications(publications => publications.AddPublication(publication => publication
    .RoutingKey("greeting")
    .RabbitMqRoutingKey("greeting")
    .CloudEventType(CloudEventType.Json)   // structured content mode
    .Exchange(exchange => exchange
        .Name("greetings")
        .CreateIfNotExists(create => create.Type("topic")))))
```

In structured mode the body is a single JSON envelope object carrying the event attributes
(`specversion`, `id`, `source`, `type`, `time`, ...), the tracing context
(`traceparent`/`tracestate`/`baggage`) and any extension attributes as top-level members,
published with the `application/cloudevents+json` content type and no `cloudEvents:*`
headers. A JSON payload is embedded as the raw `data` member; any other payload is
base64-encoded into `data_base64`.

The envelope is not produced by the transport itself: selecting `CloudEventType.Json`
registers the `StructuredCloudEventTransformer` (from the `Amanhecer` package) into the
publication's encode pipeline, where it runs last — after the attributes and defaults have
been applied — and wraps the payload. Every subscription likewise gets the transformer first
in its decode pipeline: it sniffs the `application/cloudevents+json` content type (ignoring
parameters) and parses the envelope transparently, surfacing unknown top-level members as
`cloudEvents:*` headers, matching binary mode; binary-mode messages pass through untouched.
The transport's only part in structured mode is omitting the `cloudEvents:*` headers.

## Consuming

The `Amanhecer.Extensions.Hosting` package runs a consumer for every subscription together with
the host:

```csharp
using Amanhecer.Extensions.Hosting.Extensions;

builder.Services.AddAmanhecerHost();
```

Each consumed message is decoded and dispatched through the pipeline of the subscription's
routing key, then acked; failures are settled through the subscription's `OnError`
(ack, nack, defer or move to a dead-letter/invalid-message destination).

Deferring requeues the message on the broker, which redelivers it immediately (the requested
delay is not honored by this transport). By default there is no client-side bound on
redeliveries (`MaxDeliveryAttempts` defaults to `0`), and the consumer logs a warning at
startup: a persistently failing handler spins a hot receive/fail/requeue loop unless
redelivery is bounded somewhere. Bound it either client-side — set `MaxDeliveryAttempts(n)`
per subscription so that, once a message has been delivered `n` times, deferring nacks it
without requeue and the broker dead-letters it when the queue has a dead-letter exchange
configured — or broker-side, e.g. a quorum queue with `delivery-limit` and a DLX:

```csharp
.Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
    .ToRoutingKey("greeting")
    .QueueName("greetings")
    // MaxDeliveryAttempts stays disabled (0); the broker's delivery-limit applies
    .CreateIfNotExists(create => create
        // ...
        .QueueArgument("x-queue-type", "quorum")
        .QueueArgument("x-delivery-limit", 10))))
```

The attempt count is tracked in memory and floored by the broker's own bookkeeping: the
`x-death` counts of messages that cycled through a dead-letter exchange, and the
`x-delivery-count` header quorum queues stamp on redelivery — so a consumer restart does
not reset the count for quorum queues. When both a client-side `MaxDeliveryAttempts` and a
broker-side `delivery-limit` are configured, the effective limit is whichever fires first —
both end at the same dead-letter exchange.

The subscription's channel prefetches `BufferSize + NumberOfConsumers` messages: one
buffer's worth plus one in-flight delivery per consumer. The buffer bounds how many
messages are held in memory, providing end-to-end backpressure.

## Provisioning

Exchanges and queues declare how their transport resources are set up:

- `AssumeExists()` — no provisioning (the default for exchanges).
- `ValidateIfExists()` — fail fast when the resource is missing.
- `CreateIfNotExists(...)` — declare the resource when missing. Queues accept additional
  declaration arguments via `QueueArgument`, which is how queue types such as quorum queues are
  selected:

```csharp
.CreateIfNotExists(create => create
    .Exchange(/* ... */)
    .RoutingKey("greeting")
    .Durable(true)                            // quorum queues must be durable and non-exclusive
    .QueueArgument("x-queue-type", "quorum"))
```

See `samples/RabbitMqQuorum` for a complete, runnable quorum-queue example.

Provisioning never runs while services are registered: `AddAmanhecer`/`UsingRabbitMQ` open no
broker connection, so building the service collection stays fast and cannot deadlock on a
captured `SynchronizationContext`. Instead, each gateway provisions once, the first time it is used:

- when the host starts, the `Amanhecer.Extensions.Hosting` hosted service provisions every
  registered gateway before starting its consumers;
- in publish-only applications, the first publish provisions the gateway's exchanges before
  the producer channels are created.

A failed provisioning attempt is retried on the next use. Gateway validations (duplicate
publication routing keys, invalid buffer size/consumer counts) surface at the same time —
host start or first publish — rather than during registration.

## Message mappers and transformers

Publications and subscriptions without an explicit `MessageMapper<T>()` fall back to the JSON
mapper. Encode/decode transformers wrap the mapping — see [Transformers](transformers.md).
