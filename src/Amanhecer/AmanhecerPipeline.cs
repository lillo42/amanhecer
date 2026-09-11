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
    /// <param name="middlewaresMetadata">The registration metadata of each middleware, in the
    /// same order as <paramref name="middlewares"/>; while a middleware runs, the context holds
    /// its own metadata, so registrations of the same middleware type carrying different
    /// metadata do not collide.</param>
    public AmanhecerPipeline(IReadOnlyList<IMiddleware> middlewares,
        IReadOnlyDictionary<string, object?>? metadata = null,
        IReadOnlyList<object?>? middlewaresMetadata = null)
    {
        _metadata = metadata ?? new Dictionary<string, object?>();

        Func<AmanhecerContext, ValueTask> next = static _ => new ValueTask();

        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var middlewareMetadata = middlewaresMetadata?[i];
            var continuation = next;
            next = middlewareMetadata == null
                ? context => middleware.ExecuteAsync(context, continuation)
                : context => ExecuteWithMetadataAsync(middleware, middlewareMetadata, context, continuation);
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

    private static async ValueTask ExecuteWithMetadataAsync(IMiddleware middleware,
        object middlewareMetadata,
        AmanhecerContext context,
        Func<AmanhecerContext, ValueTask> next)
    {
        var key = middlewareMetadata.GetType().FullName ?? middlewareMetadata.GetType().Name;
        var replaced = context.Metadata.TryGetValue(key, out var previous);
        context.Metadata[key] = middlewareMetadata;

        try
        {
            await middleware.ExecuteAsync(context, next).ConfigureAwait(context.ContinueOnCapturedContext);
        }
        finally
        {
            if (replaced)
            {
                context.Metadata[key] = previous;
            }
            else
            {
                context.Metadata.Remove(key);
            }
        }
    }
}