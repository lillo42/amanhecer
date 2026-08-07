using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer;

/// <summary>
/// An <see cref="IPipeline"/> implementation that runs an ordered chain of middlewares,
/// each middleware invoking the next one in the chain.
/// </summary>
/// <param name="middlewares">The ordered middlewares that compose the pipeline.</param>
public class AmanhencerPipeline(ImmutableList<IMiddleware> middlewares) : IPipeline
{
    /// <summary>
    /// Executes the pipeline middlewares, in order, for the given context.
    /// </summary>
    /// <param name="context">The pipeline context that flows through the middleware chain.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline finishes.</returns>
    public async ValueTask ExecuteAsync(IPipelineContext context) => await ExecuteAsync(context, 0);

    /// <summary>
    /// Executes the middleware at the given position and chains to the next one,
    /// stopping early when cancellation is requested.
    /// </summary>
    private async ValueTask ExecuteAsync(IPipelineContext context, int position)
    {
        if (context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (position == middlewares.Count)
        {
            return;
        }

        await middlewares[position].ExecuteAsync(context, c => ExecuteAsync(c, position + 1));
    }
}