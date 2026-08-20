using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Amanhecer.Abstractions;

/// <summary>
/// Default <see cref="IPipelineContext"/> implementation: an immutable record carrying the state
/// of a request as it flows through its pipeline.
/// </summary>
/// <param name="Activity">The <see cref="System.Diagnostics.Activity"/> used to trace the execution of the pipeline, if any.</param>
/// <param name="TelemetryTags">The additional tags to attach to the telemetry recorded while the request flows through the pipeline.</param>
/// <param name="Metadata">The metadata associated with the request being processed.</param>
/// <param name="RoutingKey">The routing key used to resolve the pipeline for the request.</param>
/// <param name="Request">The request being processed by the pipeline.</param>
/// <param name="ExecutingStrategy">The strategy used to execute the pipelines resolved for this context.</param>
/// <param name="ContinueOnCapturedContext">Whether continuations should resume on the captured
/// <see cref="System.Threading.SynchronizationContext"/> while executing the pipeline.</param>
/// <param name="CancellationToken">The token that signals cancellation of the pipeline execution.</param>
public record AmanhecerPipelineContext(
    Activity? Activity,
    List<KeyValuePair<string, object?>> TelemetryTags,
    Dictionary<string, object> Metadata,
    string RoutingKey,
    string RequestId,
    string CorrelationId,
    object Request,
    IExecutingStrategy ExecutingStrategy,
    bool ContinueOnCapturedContext,
    CancellationToken CancellationToken)
    : IPipelineContext
{
    /// <inheritdoc />
    public object? Response { get; set; }

    /// <summary>
    /// Creates a copy of this context with new, shallow-copied <see cref="TelemetryTags"/> list and
    /// <see cref="Metadata"/> dictionary, so changes to the clone do not affect the original context.
    /// </summary>
    /// <param name="activity">The activity the clone should carry; when <see langword="null"/>,
    /// the clone carries no activity.</param>
    /// <param name="cancellationToken">The token the clone should carry; when
    /// <see cref="CancellationToken.None"/>, the clone carries this context's token.</param>
    /// <returns>A new <see cref="IPipelineContext"/> with the same values as this instance.</returns>
    public IPipelineContext DeepClone(Activity? activity = null, CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == CancellationToken.None ? CancellationToken : cancellationToken;
        return this with
        {
            Activity = activity ?? Activity,
            TelemetryTags = [.. TelemetryTags],
            Metadata = new Dictionary<string, object>(Metadata),
            CancellationToken = cancellationToken
        };
    }
}