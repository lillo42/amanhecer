# Amanhecer.Abstractions

Abstractions for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET: interfaces, base classes, attributes and dispatch contexts.

This package contains the contracts your application code depends on:

- **Handlers** — derive from `RequestHandler<T>` or `QueryHandler<TQuery, TResponse>`, or implement `IRequestHandler` / `IQueryHandler` directly.
- **Dispatch** — `IDispatcher` and the `AmanhecerContext` implementation carrying routing-key overrides, metadata, an `Activity` and a per-dispatch executing strategy.
- **Pipelines and middleware** — `IPipeline`, `IMiddleware`, and the factories used to build them.
- **Attributes** — `[RoutingKey]` to override a request's routing key, and `MiddlewareAttribute` to declare middleware on handler classes or methods.
- **Exceptions** — `AmanhecerException`, `PipelineNotFoundException`, `MultiPipelineFoundException`.

You normally reference the `Amanhecer` package (which brings this one in transitively) and only depend on `Amanhecer.Abstractions` directly when building your own extensions on top of Amanhecer.

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

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/tree/main/docs).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
