using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer;

/// <summary>
/// An <see cref="IPipeline"/> implementation that runs an ordered chain of middlewares,
/// each middleware invoking the next one in the chain.
/// </summary>
public class AmanhecerPipeline : IPipeline
{
    private readonly Func<AmanhecerContext, ValueTask> _chain;
    private readonly IReadOnlyDictionary<string, object?> _metadata;

    /// <summary>
    /// An <see cref="IPipeline"/> implementation that runs an ordered chain of middlewares,
    /// each middleware invoking the next one in the chain.
    /// </summary>
    /// <param name="middlewares">The ordered middlewares that compose the pipeline.</param>
    /// <param name="metadata">The metadata collected while the pipeline's middlewares were
    /// created; merged into the context before the chain runs, so pipelines sharing a routing
    /// key do not overwrite each other's middleware metadata.</param>
    public AmanhecerPipeline(IReadOnlyList<IMiddleware> middlewares,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        _metadata = metadata ?? new Dictionary<string, object?>();

        Func<AmanhecerContext, ValueTask> next = static _ => new ValueTask();

        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var continuation = next;
            next = context => middleware.ExecuteAsync(context, continuation);
        }

        _chain = next;
    }

    /// <summary>
    /// Executes the pipeline middlewares, in order, for the given context.
    /// </summary>
    /// <param name="context">The pipeline context that flows through the middleware chain.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline finishes.</returns>
    public async ValueTask ExecuteAsync(AmanhecerContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        foreach (var pair in _metadata)
        {
            context.Metadata[pair.Key] = pair.Value;
        }

        await _chain(context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}