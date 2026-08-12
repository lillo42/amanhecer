# Resilience

Two packages execute the remainder of a pipeline inside a named Polly resilience pipeline (retry, circuit breaker, timeout, fallback, hedging):

- **`Amanhecer.Polly`** — a thin integration on top of `Polly.Core`.
- **`Amanhecer.Extensions.Resilience`** — built on `Microsoft.Extensions.Resilience`, additionally enriching Polly telemetry with request metadata.

Both expose the same shape: a middleware resolved by pipeline name, registered fluently or via attribute.

## Configuring resilience pipelines

Register named pipelines with the container (via `Polly.Extensions`, included transitively by `Microsoft.Extensions.Resilience`):

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
        .Use<PollyResiliencePipelineMiddleware>(order: 1, metadata: "order-processing")));
```

or, with the `Amanhecer.Extensions.Resilience` package:

```csharp
services.AddAmanhecer(a => a
    .AddRequestHandler<PlaceOrderHandler>(cfg => cfg
        .Use<ResiliencePipelineMiddleware>(order: 1, metadata: "order-processing")));
```

## Attribute usage

```csharp
[PollyResiliencePipeline("order-processing", order: 1)]
public class PlaceOrderHandler : RequestHandler<PlaceOrder> { ... }
```

```csharp
[ResiliencePipeline("order-processing", order: 1)]
public class PlaceOrderHandler : RequestHandler<PlaceOrder> { ... }
```

## Behaviour

- The dispatch's `CancellationToken` flows into the resilience execution, so cancelling a dispatch aborts retries, and a timeout strategy replaces the token seen by the rest of the pipeline.
- A custom Polly `ResilienceContext` can be supplied per dispatch through the pipeline-context metadata, under the key `PollyResiliencePipelineMiddleware.ResilienceContext` (or `ResiliencePipelineMiddleware.ResilienceContextKey`).

## Telemetry enrichment

With `Amanhecer.Extensions.Resilience`, the middleware enriches the execution with `RequestMetadata` carrying the request type name. Register the resilience enricher to include it (and exception summaries) in Polly's logs and metrics:

```csharp
services.AddResilienceEnricher();
```
