using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.ExecutingStrategies;

/// <summary>
/// Executes the pipelines sequentially, one after another, each with a deep-cloned
/// <see cref="IPipelineContext"/>.
/// </summary>
public class SequenceExecutingStrategy : IExecutingStrategy
{
    /// <summary>
    /// Executes the given pipelines in sequence. Does nothing when the list is empty and
    /// runs the pipeline directly when there is only one.
    /// </summary>
    /// <param name="context">The pipeline context; deep-cloned per pipeline when more than one pipeline runs.</param>
    /// <param name="pipelines">The pipelines to execute.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when all pipelines have finished.</returns>
    /// <exception cref="AggregateException">Thrown when multiple pipelines are executed and at least one of them throws; contains all thrown exceptions.</exception>
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