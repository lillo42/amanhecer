using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Amanhecer.Abstractions;

/// <summary>
/// Carries the state of a request as it flows through a pipeline: the request itself, its routing
/// key, metadata, tracing information, and the response once it has been handled.
/// </summary>
public interface IPipelineContext
{
    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Activity"/> used to trace the execution of the pipeline, if any.
    /// </summary>
    Activity? Activity { get; }

    /// <summary>
    /// Gets the additional tags to attach to the telemetry (span and metrics) recorded while
    /// the request flows through the pipeline.
    /// </summary>
    List<KeyValuePair<string, object?>> TelemetryTags { get; }

    /// <summary>
    /// Gets the metadata associated with the request being processed.
    /// </summary>
    Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the token that signals cancellation of the pipeline execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the routing key used to resolve the pipeline for the request.
    /// </summary>
    string RoutingKey { get; }
    
    /// <summary>
    /// Gets the identifier used to correlate this request with related requests.
    /// </summary>
    string CorrelationId { get; }
    
    /// <summary>
    /// Gets the unique identifier of the request being processed.
    /// </summary>
    string RequestId { get; }

    /// <summary>
    /// Gets the request being processed by the pipeline.
    /// </summary>
    object Request { get; }

    /// <summary>
    /// Gets or sets the response produced while processing the request.
    /// </summary>
    object? Response { get; set; }

    /// <summary>
    /// Gets the strategy used to execute the pipelines resolved for this context.
    /// </summary>
    IExecutingStrategy ExecutingStrategy { get; }
    
    /// <summary>
    /// Gets whether continuations should resume on the captured
    /// <see cref="System.Threading.SynchronizationContext"/> while executing the pipeline.
    /// </summary>
    bool ContinueOnCapturedContext { get; }

    /// <summary>
    /// Creates a deep copy of this context, including a copy of the <see cref="Metadata"/> dictionary.
    /// </summary>
    /// <param name="parentContext">The activity the copy should carry; when <see langword="null"/>,
    /// the copy carries no activity.</param>
    /// <param name="cancellationToken">The token the copy should carry; when
    /// <see cref="CancellationToken.None"/>, the copy carries this context's token.</param>
    /// <returns>A new <see cref="IPipelineContext"/> with the same values as this instance.</returns>
    IPipelineContext DeepClone(Activity? parentContext = null, CancellationToken cancellationToken = default);
}