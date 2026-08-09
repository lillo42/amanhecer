# Observability

Amanhencer emits traces and metrics through a built-in `ActivitySource` and `Meter`, and structured logs through `AmanhencerLoggerMiddleware`.

## OpenTelemetry

Add the `Amanhencer.OpenTelemetry` package and register the instrumentation:

```csharp
using OpenTelemetry;

var services = new ServiceCollection();

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAmanhencerInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAmanhencerInstrumentation()
        .AddConsoleExporter());
```

## Telemetry middleware

Add `AmanhencerTelemetryMiddleware` to a pipeline — fluently or with `[AmanhencerTelemetry]` — to record a span per dispatch and operation metrics:

```csharp
services.AddAmanhencer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg
        .Use<AmanhencerTelemetryMiddleware>(order: 1)));
```

Recorded metrics include counters for succeeded, failed, timed-out and cancelled requests, plus a duration histogram. Spans and metrics are tagged with the routing key, request type and operation (`send`, `post`, `query`), and any custom tags carried by `IContext.TelemetryTags`.

## Logging

Add `AmanhencerLoggerMiddleware` (or `[RequestLogging]`) for source-generated structured logs — processing/processed/cancelled/failed — scoped with the routing key and request type:

```csharp
public class GreetingHandler : RequestHandler<Greeting>
{
    [RequestLogging(3)]
    public override ValueTask HandleAsync(Greeting request, IPipelineContext context,
        CancellationToken cancellationToken = default) { ... }
}
```
