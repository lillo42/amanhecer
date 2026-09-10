# Observability

Amanhecer emits traces and metrics through a built-in `ActivitySource` and `Meter`, and structured logs through `AmanhecerLoggerMiddleware`.

## OpenTelemetry

Add the `Amanhecer.OpenTelemetry` package and register the instrumentation:

```csharp
using OpenTelemetry;

var services = new ServiceCollection();

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAmanhecerInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAmanhecerInstrumentation()
        .AddConsoleExporter());
```

## Telemetry middleware

Add `AmanhecerTelemetryMiddleware` to a pipeline — fluently or with `[AmanhecerTelemetry]` — to record a span per dispatch and operation metrics:

```csharp
services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg
        .Use<AmanhecerTelemetryMiddleware>(order: 1)));
```

Recorded metrics include counters for succeeded, failed, timed-out and cancelled requests, plus a duration histogram. Spans and metrics are tagged with the routing key, request type, executing strategy and operation (`send`, `publish`, `query`, `post`), plus any custom tags carried by `AmanhecerContext.TelemetryTags`.

## Logging

Add `AmanhecerLoggerMiddleware` (or `[RequestLogging]`) for source-generated structured logs — processing/processed/cancelled/failed — scoped with the routing key and request type:

```csharp
public class GreetingHandler : RequestHandler<Greeting>
{
    [RequestLogging(3)]
    public override ValueTask HandleAsync(Greeting request, AmanhecerContext context,
        CancellationToken cancellationToken = default) { ... }
}
```
