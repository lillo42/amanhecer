# Amanhecer

A lightweight request dispatcher (mediator) for .NET, inspired by [Paramore Brighter](https://github.com/BrighterCommand/Brighter). Send commands, publish events and execute queries through configurable middleware pipelines — with no external broker required.

## Quick start

Define a request and its handler:

```csharp
using Amanhecer.Abstractions;

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

Register Amanhecer and dispatch:

```csharp
using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddAmanhecer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();

var dispatcher = services.GetRequiredService<IDispatcher>();

dispatcher.Send(new Greeting("world"));                     // one handler
dispatcher.Publish(new OrderShipped(id));                   // any number of handlers
var answer = dispatcher.Query<Ask, string>(new Ask("?"));   // returns a response
```

All three operations have `async` overloads (`SendAsync`, `PublishAsync`, `QueryAsync`) and accept an optional `IContext` carrying a routing-key override, metadata, an `Activity` and a per-dispatch executing strategy.

## Features

- **Send / Publish / Query** — dispatch a command to a single handler, an event to any number of handlers, or a query that returns a response.
- **Middleware pipelines** — wrap handlers with cross-cutting concerns (logging, validation, retries) configured fluently or via attributes.
- **Routing keys** — route requests to pipelines by convention (type name) or explicitly with `[RoutingKey]`.
- **Executing strategies** — run published pipelines sequentially or in parallel.
- **DI-first** — built on `Microsoft.Extensions.DependencyInjection`; everything is resolved from the container.
- **Multi-targeting** — `net462`, `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`, AOT-compatible on modern targets.

## Companion packages

- `Amanhecer.Polly` — execute pipelines inside named Polly resilience pipelines.
- `Amanhecer.Extensions.Resilience` — the same, built on `Microsoft.Extensions.Resilience` with telemetry enrichment.
- `Amanhecer.OpenTelemetry` — OpenTelemetry tracing and metrics instrumentation.

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/tree/main/docs): middleware, routing, executing strategies, resilience and observability.

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
