# In-memory transport

The `Amanhecer.InMemory` package binds the messaging gateway to in-memory queues backed by
`System.Threading.Channels`. It is useful for local development, tests and broker-free
deployments where producers and consumers run in the same process.

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

The subscription's `ToRoutingKey` must match the routing key of the handler pipeline that
processes the message (the type's full name, or its `[RoutingKey]` value).

## Publishing

Post a request and the mapped `Message` is encoded and written to the configured in-memory
queue:

```csharp
var dispatcher = provider.GetRequiredService<IDispatcher>();
await dispatcher.PostAsync(new Greeting("world"));
```

## Consuming

Use `Amanhecer.Extensions.Hosting` to run consumers in a generic host:

```csharp
using Amanhecer.Extensions.Hosting.Extensions;

builder.Services.AddAmanhecerHost();
```

Each consumed message is decoded and dispatched through the pipeline of the subscription's
routing key.

## Queue provisioning

Both publications and subscriptions support the same queue-provisioning modes:

- `AssumeExists()` - no provisioning. Because the queues of this transport only exist in
  process, something else has to register the channel in the gateway's queue registry first,
  or publishing and consuming fail.
- `ValidateIfExists()` - fail if the queue is missing.
- `CreateOrOverride(...)` - create or replace the in-memory channel.

Provisioning runs once per gateway, on whichever happens first: the consumer host starting, or
the first publish. Publish-only applications therefore get their queues without
`AddAmanhecerHost()`, and restarting the consumer host does not replace the channels and
discard the messages still queued in them.

When a publication leaves `QueueName` unset, the routing key is used as the queue name.

`CreateOrOverride` accepts channel settings:

- `Capacity(n)` - `n <= 0` creates an unbounded queue.
- `FullMode(...)` - bounded queue behavior when full (`Wait`, `DropOldest`, `DropNewest`,
  `DropWrite`).

## Error handling and redelivery

`OnError(...)` configures how failures are settled:

- `Ack` - acknowledge and drop the message.
- `Nack` - negative-acknowledge and drop the message.
- `Defer(delay)` - re-enqueue the same message (delay is honored by waiting before requeue).
  The requeue is abandoned when the host stops.
- `MoveToDeadLetter` / `MoveToInvalidMessage` - repost to the routing key configured by
  `DeadLetterQueueRoutingKey(...)` or `InvalidMessageRoutingKey(...)`.

Unlike broker transports, ack/nack are local channel semantics in this transport.

## Message mappers and transformers

Publications and subscriptions without an explicit `MessageMapper<T>()` fall back to the JSON
mapper. Encode/decode transformers wrap the mapping - see [Transformers](transformers.md).
