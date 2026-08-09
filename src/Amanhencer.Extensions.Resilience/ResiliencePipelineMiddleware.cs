using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Microsoft.Extensions.Http.Diagnostics;
using Polly;
using Polly.Registry;

namespace Amanhencer.Extensions.Resilience;

/// <summary>
/// Declares <see cref="ResiliencePipelineMiddleware"/> to be included in the pipeline of the
/// annotated handler class or handler method.
/// </summary>
/// <param name="pipelineName">The name of the resilience pipeline to execute, as registered in
/// the <see cref="ResiliencePipelineProvider{TKey}"/>.</param>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
public class ResiliencePipelineAttribute(string pipelineName, int order) : MiddlewareAttribute(order)
{
    /// <summary>
    /// Gets the name of the resilience pipeline to execute.
    /// </summary>
    public string PipelineName { get; } = pipelineName;

    /// <inheritdoc />
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType()
    {
        return typeof(ResiliencePipelineMiddleware);
    }
}

/// <summary>
/// Middleware that executes the remainder of the pipeline inside a Polly
/// <see cref="ResiliencePipeline"/> resolved by name from a
/// <see cref="ResiliencePipelineProvider{TKey}"/>.
/// </summary>
/// <remarks>
/// The pipeline name is supplied via the middleware metadata: either as a string when registering
/// the middleware fluently (for example, <c>Use&lt;ResiliencePipelineMiddleware&gt;(order, "myPipeline")</c>),
/// or via <see cref="ResiliencePipelineAttribute"/> on the handler class or method.
/// If <see cref="IPipelineContext.Metadata"/> contains a Polly <see cref="Polly.ResilienceContext"/>
/// under the key <see cref="ResilienceContextKey"/>, that context is used; otherwise a context is
/// rented from the pool, carrying the pipeline context's cancellation token. The execution is
/// enriched with <see cref="RequestMetadata"/> (the request type name) so that resilience telemetry
/// registered via <c>AddResilienceEnricher()</c> includes it. The cancellation token provided by
/// Polly replaces the token carried by the cloned pipeline context passed to the next middleware.
/// </remarks>
public class ResiliencePipelineMiddleware(ResiliencePipelineProvider<string> provider) : IMiddleware
{
    /// <summary>
    /// The <see cref="IPipelineContext.Metadata"/> key under which a Polly
    /// <see cref="Polly.ResilienceContext"/> can be supplied for the pipeline execution.
    /// </summary>
    public const string ResilienceContextKey = "Amanhencer.Extensions.Resilience.ResilienceContext";

    private string? _pipelineName;

    /// <summary>
    /// Initialises the middleware with the name of the resilience pipeline to execute.
    /// </summary>
    /// <param name="metadata">The middleware metadata; must be a non-empty <see cref="string"/>
    /// containing the name of the resilience pipeline registered in the provider, or a
    /// <see cref="ResiliencePipelineAttribute"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="metadata"/> is neither a non-empty
    /// <see cref="string"/> nor a <see cref="ResiliencePipelineAttribute"/>.</exception>
    public void Initialize(object? metadata)
    {
        if (metadata is string pipelineName && !string.IsNullOrWhiteSpace(pipelineName))
        {
            _pipelineName = pipelineName;
        }
        else if (metadata is ResiliencePipelineAttribute attribute)
        {
            _pipelineName = attribute.PipelineName;
        }
        else
        {
            throw new ArgumentException(
                $"Metadata must be a non-empty string with the name of the resilience pipeline, but was '{metadata ?? "null"}'.",
                nameof(metadata));
        }
    }

    /// <summary>
    /// Executes the rest of the pipeline inside the configured resilience pipeline.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the resilience pipeline execution,
    /// including any retries, completes.</returns>
    /// <exception cref="InvalidOperationException">The middleware was not initialised with a
    /// resilience pipeline name.</exception>
    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        if (string.IsNullOrEmpty(_pipelineName))
        {
            throw new InvalidOperationException(
                $"The middleware '{nameof(ResiliencePipelineMiddleware)}' was not initialised with a resilience pipeline name. " +
                "Ensure Initialize was called with the pipeline name before executing the middleware.");
        }

        var pipeline = provider.GetPipeline(_pipelineName!);

        if (context.Metadata.TryGetValue(ResilienceContextKey, out var value) &&
            value is ResilienceContext providedContext)
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

    private static async ValueTask ExecuteNextAsync(
        IPipelineContext context,
        Func<IPipelineContext, ValueTask> next,
        CancellationToken cancellationToken)
    {
        var clone = context.DeepClone(cancellationToken: cancellationToken);
        await next(clone);
    }
}

internal static class ResilienceContextMetadataExtensions
{
    /// <summary>
    /// Enriches the resilience context with <see cref="RequestMetadata"/> carrying the request
    /// type name, unless the context already has request metadata set by the caller.
    /// </summary>
    public static void SetRequestMetadataIfMissing(this ResilienceContext resilienceContext, IPipelineContext context)
    {
        if (resilienceContext.GetRequestMetadata() is null)
        {
            resilienceContext.SetRequestMetadata(new RequestMetadata { RequestName = context.Request.GetType().Name });
        }
    }
}
