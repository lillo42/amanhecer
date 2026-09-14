# Getting started

Define a request and its handler:

```csharp
using Amanhecer.Abstractions;

public record Greeting(string Name);

public class GreetingHandler : RequestHandler<Greeting>
{
    public override ValueTask HandleAsync(Greeting request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello {request.Name}!");
        return ValueTask.CompletedTask;
    }
}
```

A query handler returns a response:

```csharp
public record Ask(string Question);

public class AskHandler : QueryHandler<Ask, string>
{
    public override ValueTask<string> HandleAsync(Ask query, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult("42");
    }
}
```

## Registering handlers

Register Amanhecer and your handlers with the container:

```csharp
using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddAmanhecer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();
```

`AddRequestHandler` and `AddQueryHandler` accept an optional configuration callback used to build the middleware pipeline for that handler — see [Middleware](middleware.md).

## Dispatching

Resolve `IDispatcher` and dispatch:

```csharp
var dispatcher = services.GetRequiredService<IDispatcher>();

dispatcher.Send(new Greeting("world"));                     // exactly one pipeline required
dispatcher.Publish(new OrderShipped(id));                   // any number of pipelines (zero is fine)
var answer = dispatcher.Query<Ask, string>(new Ask("?"));   // returns a response
await dispatcher.PostAsync(new Greeting("world"));          // produces the message through the configured transport
```

`Post/PostAsync` dispatch through the configured messaging gateway transport: use
[In-memory transport](in-memory.md) for channel-backed local queues or [RabbitMQ](rabbitmq.md)
for broker-backed queues.

All operations have `async` overloads (`SendAsync`, `PublishAsync`, `QueryAsync`, `PostAsync`) and accept an optional `AmanhecerContext` carrying:

- a routing-key override (see [Routing keys](routing.md)),
- a metadata dictionary visible to middleware and handlers,
- an `Activity` for distributed tracing,
- a per-dispatch executing strategy (see [Executing strategies](executing-strategies.md)).

```csharp
await dispatcher.SendAsync(request, new AmanhecerContext
{
    Metadata = { ["tenant"] = "acme" }
}, cancellationToken);
```
