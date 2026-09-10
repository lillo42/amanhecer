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

## Message mappers and transformers

Publications and subscriptions without an explicit `MessageMapper<T>()` fall back to the JSON
mapper. Encode/decode transformers wrap the mapping — see [Transformers](transformers.md).
