using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.ExecutingStrategies;

/// <summary>
/// Executes the pipelines in parallel, each with a deep-cloned <see cref="IPipelineContext"/>.
/// </summary>
/// <param name="options">The options that control parallelism and cancellation.</param>
public class ParallelExecutingStrategy(ParallelOptions options) : IExecutingStrategy
{
    /// <summary>
    /// Executes the given pipelines in parallel. Does nothing when the list is empty and
    /// runs the pipeline directly when there is only one.
    /// </summary>
    /// <param name="context">The pipeline context; deep-cloned per pipeline when running in parallel.</param>
    /// <param name="pipelines">The pipelines to execute.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when all pipelines have finished.</returns>
    public async ValueTask ExecuteAsync(IPipelineContext context, ImmutableList<IPipeline> pipelines)
    {
        if (pipelines.Count == 0)
        {
            return;
        }

        if (pipelines.Count == 1)
        {
            foreach (var pipeline in pipelines)
            {
                await pipeline.ExecuteAsync(context);
            }

            return;
        }

#if NET8_0_OR_GREATER
        await Parallel.ForEachAsync(pipelines, options, (pipeline, _) =>
        {
            var newContext = context.DeepClone();
            return pipeline.ExecuteAsync(newContext);
        });
#else
        Parallel.ForEach(pipelines, options, (pipeline, _) =>
        {
            var newContext = context.DeepClone();
            var response = pipeline.ExecuteAsync(newContext);
            if (response.IsCompleted)
            {
                response.GetAwaiter().GetResult();
            }
            else
            {
                response.AsTask().GetAwaiter().GetResult();
            }
        });
#endif
    }
}