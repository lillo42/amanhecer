using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Microsoft.Extensions.Logging;

namespace Amanhencer.ExecutingStrategies;

/// <summary>
/// Executes the pipelines sequentially, one after another, each with a deep-cloned
/// <see cref="IPipelineContext"/>.
/// </summary>
public partial class SequenceExecutingStrategy(
    AmanhencerPipelineContextAccessor accessor,
    ILogger<SequenceExecutingStrategy> logger) : IExecutingStrategy
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
            Logger.NoPipeline(logger, context.RoutingKey);
            return;
        }

        if (pipelines.Count == 1)
        {
            Logger.OnlyOnePipeline(logger, context.RoutingKey);
            accessor.PipelineContext = context;
            try
            {
                var pipeline = pipelines[0];
                await pipeline.ExecuteAsync(context).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            finally
            {
                accessor.PipelineContext = null;
            }

            return;
        }

        Logger.MultiPipelineFound(logger, context.RoutingKey);

        var exceptions = new List<Exception>();
        foreach (var pipeline in pipelines)
        {
            var newContext = context.DeepClone();
            accessor.PipelineContext = newContext;

            try
            {
                await pipeline.ExecuteAsync(newContext).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
            finally
            {
                accessor.PipelineContext = null;
            }
        }

        if (exceptions.Count > 0)
        {
            Logger.ExceptionsWasThrowOnMultiPipeline(logger, context.RoutingKey, exceptions.Count);
            throw new AggregateException(exceptions);
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning, "No pipeline to be process for '{RoutingKey}'")]
        public static partial void NoPipeline(ILogger logger, string routingKey);

        [LoggerMessage(LogLevel.Debug, "Only one pipeline to be process for '{RoutingKey}'")]
        public static partial void OnlyOnePipeline(ILogger logger, string routingKey);

        [LoggerMessage(LogLevel.Debug, "Multi-pipeline to be process for '{RoutingKey}'")]
        public static partial void MultiPipelineFound(ILogger logger, string routingKey);

        [LoggerMessage(LogLevel.Debug,
            "An exception ({NumberOfExceptions}) was throw in multi-pipeline to be process for '{RoutingKey}'")]
        public static partial void ExceptionsWasThrowOnMultiPipeline(ILogger logger, string routingKey,
            int numberOfExceptions);
    }
}