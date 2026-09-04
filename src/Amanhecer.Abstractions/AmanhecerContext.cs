using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Amanhecer.Abstractions.Options;

namespace Amanhecer.Abstractions;

/// <summary>
/// Carries the state of a request as it flows through a pipeline: the request itself, its routing
/// key, metadata, tracing information, and the response once it has been handled.
/// </summary>
public class AmanhecerContext : ICloneable
{
    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Activity"/> used to trace the execution of the pipeline, if any.
    /// </summary>
    public Activity? Activity { get; set; }

    /// <summary>
    /// Gets the additional tags to attach to the telemetry (span and metrics) recorded while
    /// the request flows through the pipeline.
    /// </summary>
    public List<KeyValuePair<string, object?>> TelemetryTags { get; set; } = [];

    /// <summary>
    /// Gets the metadata associated with the request being processed.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets the token that signals cancellation of the pipeline execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets the routing key used to resolve the pipeline for the request.
    /// </summary>
    public string RoutingKey { get; set; } = "";

    /// <summary>
    /// Gets the identifier used to correlate this request with related requests.
    /// </summary>
    public string CorrelationId { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets the unique identifier of the request being processed.
    /// </summary>
    public string RequestId { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets the request being processed by the pipeline.
    /// </summary>
    public object Request { get; set; } = null!;

    /// <summary>
    /// Gets or sets the response produced while processing the request.
    /// </summary>
    public object? Response { get; set; }

    /// <summary>
    /// Gets the strategy used to execute the pipelines resolved for this context.
    /// </summary>
    public IExecutingStrategy? ExecutingStrategy { get; set; }

    /// <summary>
    /// Gets whether continuations should resume on the captured
    /// <see cref="System.Threading.SynchronizationContext"/> while executing the pipeline.
    /// </summary>
    public bool ContinueOnCapturedContext { get; set; }

    public List<AmanhecerMiddlewareOptions>? Middlewares { get; set; }

    /// <inheritdoc/>
    public object Clone()
    {
        return new AmanhecerContext
        {
            Activity = Activity,
            TelemetryTags = [.. TelemetryTags],
            Metadata = new Dictionary<string, object?>(Metadata),
            CancellationToken = CancellationToken,
            CorrelationId = CorrelationId,
            Request = Request,
            Response = Response,
            RequestId = RequestId,
            ContinueOnCapturedContext = ContinueOnCapturedContext,
            RoutingKey = RoutingKey
        };
    }
}