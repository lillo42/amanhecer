# Executing strategies

When `Publish` fans out to multiple pipelines, an executing strategy controls how those pipelines run.

## Sequential (default)

`SequenceExecutingStrategy` runs the pipelines in registration order. When more than one pipeline runs, each pipeline receives a shallow clone of the context, so metadata mutations don't leak between pipelines (`Request`, `Response`, `Activity` and `ExecutingStrategy` remain shared references). Failures don't stop the remaining pipelines; they are aggregated and thrown as an `AggregateException` after all pipelines have run.

## Parallel

`ParallelExecutingStrategy` runs the pipelines concurrently (via `Parallel.ForEachAsync` on modern targets). Its constructor takes the `ParallelOptions`, an `AmanhecerPipelineContextAccessor` and a logger:

```csharp
using Amanhecer.ExecutingStrategies;
using Microsoft.Extensions.Logging.Abstractions;

services.AddAmanhecer(a => a
    .SetExecutorStrategy(new ParallelExecutingStrategy(
        new ParallelOptions { MaxDegreeOfParallelism = 4 },
        new AmanhecerPipelineContextAccessor(),
        NullLogger<ParallelExecutingStrategy>.Instance))
    .AddRequestHandler<GreetingHandler>());
```

## Per-dispatch override

Pass a strategy on the dispatch context to override the global one for a single call:

```csharp
await dispatcher.PublishAsync(@event, new AmanhecerContext
{
    ExecutingStrategy = new ParallelExecutingStrategy(
        new ParallelOptions(),
        new AmanhecerPipelineContextAccessor(),
        NullLogger<ParallelExecutingStrategy>.Instance)
});
```
