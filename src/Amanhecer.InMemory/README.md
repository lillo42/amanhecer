# Amanhecer.InMemory

In-memory transport for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET. Bind the messaging gateway to channel-backed queues, so `Post`/`PostAsync` can publish and consume messages without an external broker.

## Configuring the gateway

Configure publications and subscriptions through `UsingInMemory`:

```csharp
using Amanhecer.Configurator;
using Amanhecer.Extensions;

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingInMemory(inMemory => inMemory
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey("greeting")
            .QueueName("greetings")
            .CreateOrOverride(create => create.Capacity(1024))))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey("greeting")
            .QueueName("greetings")
            .CreateOrOverride(create => create.Capacity(1024)))))));
```

## Queue provisioning

- `AssumeExists()` - no provisioning.
- `ValidateIfExists()` - fail if the queue is missing.
- `CreateOrOverride(...)` - create or replace the in-memory channel.

`CreateOrOverride` supports `Capacity(...)` and `FullMode(...)` for bounded channels.

## Error handling

`OnError(...)` configures failure settlement (`Ack`, `Nack`, `Defer`, `MoveToDeadLetter`, `MoveToInvalidMessage`). For moves, set `DeadLetterQueueRoutingKey(...)` and/or `InvalidMessageRoutingKey(...)` on the subscription.

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/blob/main/docs/in-memory.md).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
