# Executing strategies

When `Publish` fans out to multiple pipelines, an executing strategy controls how those pipelines run.

## Sequential (default)

`SequenceExecutingStrategy` runs the pipelines in registration order. Each pipeline receives a deep clone of the context, so mutations don't leak between pipelines. Failures don't stop the remaining pipelines; they are aggregated and thrown as an `AggregateException` after all pipelines have run.

## Parallel

`ParallelExecutingStrategy` runs the pipelines concurrently (via `Parallel.ForEachAsync` on modern targets):

```csharp
services.AddAmanhencer(a => a
    .SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions
    {
        MaxDegreeOfParallelism = 4
    }))
    .AddRequestHandler<GreetingHandler>());
```

## Per-dispatch override

Pass a strategy on the dispatch context to override the global one for a single call:

```csharp
await dispatcher.PublishAsync(@event, new AmanhencerContext
{
    ExecutingStrategy = new ParallelExecutingStrategy(new ParallelOptions())
});
```
