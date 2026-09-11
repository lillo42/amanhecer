# Amanhecer.OpenTelemetry

OpenTelemetry instrumentation for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET. Collect traces and metrics emitted by Amanhecer's built-in `ActivitySource` and `Meter`.

## Registering the instrumentation

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

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/blob/main/docs/observability.md).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
