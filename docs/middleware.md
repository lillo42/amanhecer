# Middleware

Middleware wraps the handler in a Russian-doll pipeline: each middleware can run code before and after calling the next step, short-circuit the pipeline, or swallow/replace exceptions.

Implement `IMiddleware`:

```csharp
using Amanhecer.Abstractions;

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
```

`Initialize` is called once when the middleware is created and receives the metadata supplied at registration time.

## Fluent registration

Add middleware to a handler's pipeline when registering it:

```csharp
services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg
        .Use<LoggingMiddleware>(order: 1)
        .Use<TimingMiddleware>(order: 2, metadata: "slow")));
```

## Attribute registration

Derive from `MiddlewareAttribute` and annotate the handler class or its `HandleAsync` method:

```csharp
public class LoggingAttribute(int order) : MiddlewareAttribute(order)
{
    public override Type GetMiddlewareType() => typeof(LoggingMiddleware);
}

[Logging(order: 1)]
public class GreetingHandler : RequestHandler<Greeting> { ... }
```

With attribute registration, the attribute instance itself is passed to `Initialize` as the metadata, so the attribute can carry configuration to the middleware.

## Ordering

Lower `Order` values run earlier; the handler always runs last. Fluent and attribute-declared middleware are merged into a single ordered pipeline.

## Built-in middleware

The `Amanhecer` package ships with:

- `AmanhecerLoggerMiddleware` (+ `[RequestLogging]`) — structured, source-generated logs for processing/processed/cancelled/failed, scoped with the routing key and request type.
- `AmanhecerTelemetryMiddleware` (+ `[AmanhecerTelemetry]`) — spans and metrics; see [Observability](observability.md).

The `Amanhecer.Polly` and `Amanhecer.Extensions.Resilience` packages add resilience middleware — see [Resilience](resilience.md).
