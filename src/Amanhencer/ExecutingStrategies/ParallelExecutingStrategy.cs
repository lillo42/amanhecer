using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.ExecutingStrategies;

public class ParallelExecutingStrategy(ParallelOptions options) : IExecutingStrategy
{
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