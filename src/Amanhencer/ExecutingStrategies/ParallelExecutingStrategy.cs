using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Microsoft.Extensions.Logging;

namespace Amanhencer.ExecutingStrategies;

/// <summary>
/// Executes the pipelines in parallel, each with a deep-cloned <see cref="IPipelineContext"/>.
/// </summary>
/// <param name="options">The options that control parallelism and cancellation.</param>
/// <param name="accessor">Exposes the <see cref="IPipelineContext"/> of the pipeline currently executing.</param>
/// <param name="logger">The logger used to record execution diagnostics.</param>
public partial class ParallelExecutingStrategy(
    ParallelOptions options,
    AmanhencerPipelineContextAccessor accessor,
    ILogger<ParallelExecutingStrategy> logger)
    : IExecutingStrategy
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

        var exceptions = new ConcurrentBag<Exception>();

#if NET8_0_OR_GREATER
        await Parallel.ForEachAsync(pipelines, options, async (pipeline, _) =>
        {
            try
            {
                var tmpContext = context.DeepClone();
                accessor.PipelineContext = tmpContext;
                
                await pipeline.ExecuteAsync(tmpContext).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
            finally
            {
                accessor.PipelineContext = null;
            }
        });
#else
        Parallel.ForEach(pipelines, options, (pipeline, _) =>
        {
            try
            {
                var tmpContext = context.DeepClone();
                accessor.PipelineContext = tmpContext;

                var response = pipeline.ExecuteAsync(tmpContext);
                if (response.IsCompleted)
                {
                    response.GetAwaiter().GetResult();
                }
                else
                {
                    response.AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
            finally
            {
                accessor.PipelineContext = null;
            }
        });
#endif

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