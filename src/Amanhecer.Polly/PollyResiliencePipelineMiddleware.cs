using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Polly;
using Polly.Registry;

namespace Amanhecer.Polly;

/// <summary>
/// Declares <see cref="PollyResiliencePipelineMiddleware"/> to be included in the pipeline of the
/// annotated handler class or handler method.
/// </summary>
/// <param name="pipelineName">The name of the resilience pipeline to execute, as registered in
/// the <see cref="ResiliencePipelineProvider{TKey}"/>.</param>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
public class PollyResiliencePipelineAttribute(string pipelineName, int order)
    : MiddlewareAttribute<PollyResiliencePipelineMiddleware>(order)
{
    /// <summary>
    /// Gets the name of the resilience pipeline to execute.
    /// </summary>
    public string PipelineName { get; } = pipelineName;
}

/// <summary>
/// The middleware metadata that carries the resilience pipeline name when
/// <see cref="PollyResiliencePipelineMiddleware"/> is registered fluently, for example
/// <c>Use&lt;PollyResiliencePipelineMiddleware&gt;(order, new PollyPipelineMetadata("myPipeline"))</c>.
/// </summary>
/// <param name="PipelineName">The name of the resilience pipeline to execute, as registered in
/// the <see cref="ResiliencePipelineProvider{TKey}"/>.</param>
public record PollyPipelineMetadata(string PipelineName);

/// <summary>
/// Middleware that executes the remainder of the pipeline inside a Polly
/// <see cref="ResiliencePipeline"/> resolved by name from a
/// <see cref="ResiliencePipelineProvider{TKey}"/>.
/// </summary>
/// <remarks>
/// The pipeline name is supplied via the middleware metadata: either as a
/// <see cref="PollyPipelineMetadata"/> when registering the middleware fluently,
/// or via <see cref="PollyResiliencePipelineAttribute"/> on the handler class or method.
/// If <see cref="AmanhecerContext.Metadata"/> contains a Polly <see cref="T:Polly.ResilienceContext"/>
/// under the key <see cref="ResilienceContext"/>, that context is used; otherwise a context is
/// rented from the pool, carrying the pipeline context's cancellation token. Each execution
/// attempt runs against a clone of the pipeline context whose cancellation token is the one
/// provided by Polly, so mutations made by a failed attempt do not leak into the next attempt;
/// the successful attempt's response is copied back to the original context.
/// </remarks>
public class PollyResiliencePipelineMiddleware(ResiliencePipelineProvider<string> provider) : IMiddleware
{
    /// <summary>
    /// The <see cref="AmanhecerContext.Metadata"/> key under which a Polly
    /// <see cref="T:Polly.ResilienceContext"/> can be supplied for the pipeline execution.
    /// </summary>
    public const string ResilienceContext = "Amanhecer.Polly.Resilience";

    /// <summary>
    /// Executes the rest of the pipeline inside the configured resilience pipeline.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the resilience pipeline execution,
    /// including any retries, completes.</returns>
    /// <exception cref="InvalidOperationException">No resilience pipeline name was supplied via
    /// middleware metadata or <see cref="PollyResiliencePipelineAttribute"/>.</exception>
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        var pipelineName = GetPipelineName(context);
        if (string.IsNullOrEmpty(pipelineName))
        {
            throw new InvalidOperationException(
                $"The middleware '{nameof(PollyResiliencePipelineMiddleware)}' requires a resilience pipeline name: " +
                $"register it with a '{nameof(PollyPipelineMetadata)}' as metadata (for example, Use<{nameof(PollyResiliencePipelineMiddleware)}>(order, new {nameof(PollyPipelineMetadata)}(\"myPipeline\"))) " +
                $"or annotate the handler with '{nameof(PollyResiliencePipelineAttribute)}'.");
        }

        var pipeline = provider.GetPipeline(pipelineName!);
        var resilienceContext = context.GetMetadata<ResilienceContext>(ResilienceContext);

        if (resilienceContext != null)
        {
            await pipeline.ExecuteAsync(rc => ExecuteNextAsync(context, next, rc.CancellationToken), resilienceContext)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
        else
        {
            var rentedContext = ResilienceContextPool.Shared.Get(context.CancellationToken);
            try
            {
                await pipeline.ExecuteAsync(rc => ExecuteNextAsync(context, next, rc.CancellationToken), rentedContext)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(rentedContext);
            }
        }
    }

    private static string? GetPipelineName(AmanhecerContext context)
    {
        var attribute = context.GetMetadata<PollyResiliencePipelineAttribute>();
        if (attribute != null)
        {
            return attribute.PipelineName;
        }

        return context.GetMetadata<PollyPipelineMetadata>()?.PipelineName;
    }


    private static async ValueTask ExecuteNextAsync(
        AmanhecerContext context,
        Func<AmanhecerContext, ValueTask> next,
        CancellationToken cancellationToken)
    {
        var attempt = (AmanhecerContext)context.Clone();
        attempt.CancellationToken = cancellationToken;
        await next(attempt).ConfigureAwait(context.ContinueOnCapturedContext);
        context.Response = attempt.Response;
    }
}