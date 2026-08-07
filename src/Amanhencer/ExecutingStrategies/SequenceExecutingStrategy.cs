using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.ExecutingStrategies;

public class SequenceExecutingStrategy : IExecutingStrategy
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

        var exceptions = new List<Exception>();
        foreach (var pipeline in pipelines)
        {
            var newContext = context.DeepClone();

            try
            {
                await pipeline.ExecuteAsync(newContext);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}