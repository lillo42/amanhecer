# Amanhecer.Extensions.Resilience

Resilience middleware for [Amanhecer](https://github.com/lillo42/amanhecer), a lightweight request dispatcher (mediator) for .NET, built on `Microsoft.Extensions.Resilience`. Execute the remainder of a request pipeline — including the handler — inside a named Polly resilience pipeline (retry, circuit breaker, timeout, fallback, hedging), with request metadata enrichment for Polly's telemetry.

## Configuring resilience pipelines

Register named pipelines with the container:

```csharp
services.AddResiliencePipeline("order-processing", builder => builder
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(200)
    })
    .AddTimeout(TimeSpan.FromSeconds(5)));
```

## Fluent usage

Pass the pipeline name as the middleware metadata:

```csharp
services.AddAmanhecer(a => a
    .AddRequestHandler<PlaceOrderHandler>(cfg => cfg
        .Use<ResiliencePipelineMiddleware>(order: 1, metadata: "order-processing")));
```

## Attribute usage

```csharp
[ResiliencePipeline("order-processing", order: 1)]
public class PlaceOrderHandler : RequestHandler<PlaceOrder> { ... }
```

## Behaviour

- The dispatch's `CancellationToken` flows into the resilience execution, so cancelling a dispatch aborts retries, and a timeout strategy replaces the token seen by the rest of the pipeline.
- A custom Polly `ResilienceContext` can be supplied per dispatch through the pipeline-context metadata, under the key `ResiliencePipelineMiddleware.ResilienceContext`.

## Telemetry enrichment

The middleware enriches the execution with `RequestMetadata` carrying the request type name. Register the resilience enricher to include it (and exception summaries) in Polly's logs and metrics:

```csharp
services.AddResilienceEnricher();
```

Want a thinner integration directly on `Polly.Core`? Use the `Amanhecer.Polly` package instead.

## Documentation

Full documentation lives in the [repository docs](https://github.com/lillo42/amanhecer/blob/main/docs/resilience.md).

## Licence

[LGPL-3.0-or-later](https://github.com/lillo42/amanhecer/blob/main/LICENSE)
