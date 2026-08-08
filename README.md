# Amanhencer

A lightweight request dispatcher (mediator) for .NET, inspired by [Paramore Brighter](https://github.com/BrighterCommand/Brighter). Send commands, publish events and execute queries through configurable middleware pipelines — with no external broker required.

## Features

- **Send / Publish / Query** — dispatch a command to a single handler, an event to any number of handlers, or a query that returns a response.
- **Middleware pipelines** — wrap handlers with cross-cutting concerns (logging, validation, retries) configured fluently or via attributes.
- **Routing keys** — route requests to pipelines by convention (type name) or explicitly with `[RoutingKey]`.
- **Executing strategies** — run published pipelines sequentially or in parallel.
- **DI-first** — built on `Microsoft.Extensions.DependencyInjection`; everything is resolved from the container.
- **Multi-targeting** — `net462`, `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`, AOT-compatible on modern targets.

## Quick start

Define a request and its handler:

```csharp
using Amanhencer.Abstractions;

public record Greeting(string Name);

public class GreetingHandler : RequestHandler<Greeting>
{
    public override ValueTask HandleAsync(Greeting request, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello {request.Name}!");
        return ValueTask.CompletedTask;
    }
}
```

Register Amanhencer and dispatch:

```csharp
using Amanhencer.Abstractions;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddAmanhencer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();

var dispatcher = services.GetRequiredService<IDispatcher>();

dispatcher.Send(new Greeting("world"));                     // one handler
dispatcher.Publish(new OrderShipped(id));                   // any number of handlers
var answer = dispatcher.Query<Ask, string>(new Ask("?"));   // returns a response
```

All three operations have `async` overloads (`SendAsync`, `PublishAsync`, `QueryAsync`) and accept an optional `IContext` carrying a routing-key override, metadata, an `Activity` and a per-dispatch executing strategy.

## Middleware

Implement `IMiddleware` and add it to a pipeline when registering the handler:

```csharp
public class LoggingMiddleware : IMiddleware
{
    public void Initialize(object? metadata) { }

    public async ValueTask ExecuteAsync(IPipelineContext context,
        Func<IPipelineContext, ValueTask> next)
    {
        Console.WriteLine($"--> {context.RoutingKey}");
        await next(context);
        Console.WriteLine($"<-- {context.RoutingKey}");
    }
}

services.AddAmanhencer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg.Use<LoggingMiddleware>(order: 1)));
```

Alternatively, declare middleware on the handler (class or `HandleAsync` method) by deriving from `MiddlewareAttribute`:

```csharp
public class LoggingAttribute(int order) : MiddlewareAttribute(order)
{
    public override Type GetMiddlewareType() => typeof(LoggingMiddleware);
}

[Logging(order: 1)]
public class GreetingHandler : RequestHandler<Greeting> { ... }
```

Lower `Order` values run earlier; the handler always runs last.

## Routing keys

By default a request is routed by its type's full name. Override with `[RoutingKey]`:

```csharp
[RoutingKey("priority.order")]
public record PlaceOrder(int Id);
```

or per dispatch via the context:

```csharp
dispatcher.Send(request, new AmanhencerContext { RoutingKey = "priority.order" });
```

`Send` and `Query` require exactly one matching pipeline (`PipelineNotFoundException` / `MultiPipelineFoundException` otherwise); `Publish` tolerates any number.

## Executing strategies

When `Publish` fans out to multiple pipelines, the default `SequenceExecutingStrategy` runs them in registration order. Switch to parallel execution globally:

```csharp
services.AddAmanhencer(a => a
    .SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions()))
    .AddRequestHandler<GreetingHandler>());
```

## Project layout

- `src/Amanhencer.Abstractions` — interfaces, base classes, attributes and contexts.
- `src/Amanhencer` — the dispatcher, pipeline, factories, configurators and DI extensions.
- `samples/Simple` — a minimal console example.
- `tests/Amanhencer.Tests` — unit tests (TUnit + NSubstitute).
- `tests/Amanhencer.IntegrationTests` — end-to-end tests through the real DI container and pipelines.

## Building and testing

```bash
dotnet build Amanhencer.slnx
```

The test projects use TUnit with Microsoft.Testing.Platform; run them directly (the classic `dotnet test` VSTest target is not supported here):

```bash
dotnet run --project tests/Amanhencer.Tests -f net10.0
dotnet run --project tests/Amanhencer.IntegrationTests -f net10.0
```

## Licence

[LGPL-3.0-or-later](LICENSE)
