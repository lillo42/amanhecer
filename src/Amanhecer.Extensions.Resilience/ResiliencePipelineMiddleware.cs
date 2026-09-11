using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Microsoft.Extensions.Http.Diagnostics;
using Polly;
using Polly.Registry;

namespace Amanhecer.Extensions.Resilience;

/// <summary>
/// Declares <see cref="ResiliencePipelineMiddleware"/> to be included in the pipeline of the
/// annotated handler class or handler method.
/// </summary>
/// <param name="pipelineName">The name of the resilience pipeline to execute, as registered in
/// the <see cref="ResiliencePipelineProvider{TKey}"/>.</param>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
public class ResiliencePipelineAttribute(string pipelineName, int order)
    : MiddlewareAttribute<ResiliencePipelineMiddleware>(order)
{
    /// <summary>
    /// Gets the name of the resilience pipeline to execute.
    /// </summary>
    public string PipelineName { get; } = pipelineName;
}

/// <summary>
/// The middleware metadata that carries the resilience pipeline name when
/// <see cref="ResiliencePipelineMiddleware"/> is registered fluently, for example
/// <c>Use&lt;ResiliencePipelineMiddleware&gt;(order, new ResiliencePipelineMetadata("myPipeline"))</c>.
/// </summary>
/// <param name="PipelineName">The name of the resilience pipeline to execute, as registered in
/// the <see cref="ResiliencePipelineProvider{TKey}"/>.</param>
public record ResiliencePipelineMetadata(string PipelineName);

/// <summary>
/// Middleware that executes the remainder of the pipeline inside a Polly
/// <see cref="ResiliencePipeline"/> resolved by name from a
/// <see cref="ResiliencePipelineProvider{TKey}"/>.
/// </summary>
/// <remarks>
/// The pipeline name is supplied via the middleware metadata: either as a
/// <see cref="ResiliencePipelineMetadata"/> when registering the middleware fluently,
/// or via <see cref="ResiliencePipelineAttribute"/> on the handler class or method.
/// If <see cref="AmanhecerContext.Metadata"/> contains a Polly <see cref="Polly.ResilienceContext"/>
/// under the key <see cref="ResilienceContext"/>, that context is used; otherwise a context is
/// rented from the pool, carrying the pipeline context's cancellation token. The execution is
/// enriched with <see cref="RequestMetadata"/> (the request type name) so that resilience telemetry
/// registered via <c>AddResilienceEnricher()</c> includes it. Each execution attempt runs against a
/// clone of the pipeline context whose cancellation token is the one provided by Polly, so
/// mutations made by a failed attempt do not leak into the next attempt; the successful attempt's
/// response is copied back to the original context.
/// </remarks>
public class ResiliencePipelineMiddleware(ResiliencePipelineProvider<string> provider) : IMiddleware
{
    /// <summary>
    /// The <see cref="AmanhecerContext.Metadata"/> key under which a Polly
    /// <see cref="Polly.ResilienceContext"/> can be supplied for the pipeline execution.
    /// </summary>
    public const string ResilienceContext = "Amanhecer.Extensions.Resilience.ResilienceContext";

    /// <summary>
    /// Executes the rest of the pipeline inside the configured resilience pipeline.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the resilience pipeline execution,
    /// including any retries, completes.</returns>
    /// <exception cref="InvalidOperationException">No resilience pipeline name was supplied via
    /// middleware metadata or <see cref="ResiliencePipelineAttribute"/>.</exception>
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        var pipelineName = GetPipelineName(context);
        if (string.IsNullOrEmpty(pipelineName))
        {
            throw new InvalidOperationException(
                $"The middleware '{nameof(ResiliencePipelineMiddleware)}' requires a resilience pipeline name: " +
                $"register it with a '{nameof(ResiliencePipelineMetadata)}' as metadata (for example, Use<{nameof(ResiliencePipelineMiddleware)}>(order, new {nameof(ResiliencePipelineMetadata)}(\"myPipeline\"))) " +
                $"or annotate the handler with '{nameof(ResiliencePipelineAttribute)}'.");
        }

        var pipeline = provider.GetPipeline(pipelineName!);
        var providedContext = context.GetMetadata<ResilienceContext>(ResilienceContext);

        if (providedContext != null)
        {
            providedContext.SetRequestMetadataIfMissing(context);
            await pipeline.ExecuteAsync(
                async resilienceContext => await ExecuteNextAsync(context, next, resilienceContext.CancellationToken),
                providedContext);
        }
        else
        {
            var resilienceContext = ResilienceContextPool.Shared.Get(context.CancellationToken);
            resilienceContext.SetRequestMetadataIfMissing(context);
            try
            {
                await pipeline.ExecuteAsync(
                    async rc => await ExecuteNextAsync(context, next, rc.CancellationToken),
                    resilienceContext);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(resilienceContext);
            }
        }
    }

    private static string? GetPipelineName(AmanhecerContext context)
    {
        var attribute = context.GetMetadata<ResiliencePipelineAttribute>();
        if (attribute != null)
        {
            return attribute.PipelineName;
        }

        return context.GetMetadata<ResiliencePipelineMetadata>()?.PipelineName;
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

internal static class ResilienceContextMetadataExtensions
{
    /// <summary>
    /// Enriches the resilience context with <see cref="RequestMetadata"/> carrying the request
    /// type name, unless the context already has request metadata set by the caller.
    /// </summary>
    public static void SetRequestMetadataIfMissing(this ResilienceContext resilienceContext, AmanhecerContext context)
    {
        if (resilienceContext.GetRequestMetadata() is null)
        {
            resilienceContext.SetRequestMetadata(new RequestMetadata
            {
                RequestName = context.Request.GetType().Name,
            });
        }
    }
}