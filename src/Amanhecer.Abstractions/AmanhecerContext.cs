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
    /// Gets or sets the <see cref="System.Diagnostics.Activity"/> used to trace the execution of
    /// the pipeline, if any.
    /// </summary>
    public Activity? Activity { get; set; }

    /// <summary>
    /// Gets or sets the additional tags to attach to the telemetry (span and metrics) recorded
    /// while the request flows through the pipeline.
    /// </summary>
    public List<KeyValuePair<string, object?>> TelemetryTags { get; set; } = [];

    /// <summary>
    /// Gets or sets the metadata associated with the request being processed.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the token that signals cancellation of the pipeline execution. The dispatcher
    /// assigns the caller's token to this property when the request enters the pipeline.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets or sets the routing key used to resolve the pipeline for the request. When left empty,
    /// the dispatcher resolves it from the request type's <see cref="RoutingKeyAttribute"/>,
    /// falling back to the request type's full name.
    /// </summary>
    public string RoutingKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier used to correlate this request with related requests.
    /// </summary>
    public string CorrelationId { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the unique identifier of the request being processed.
    /// </summary>
    public string RequestId { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the request being processed by the pipeline.
    /// </summary>
    public object Request { get; set; } = null!;

    /// <summary>
    /// Gets or sets the response produced while processing the request.
    /// </summary>
    public object? Response { get; set; }

    /// <summary>
    /// Gets or sets the strategy used to execute the pipelines resolved for this context. When
    /// <see langword="null"/>, the dispatcher falls back to the strategy registered in the
    /// dependency injection container.
    /// </summary>
    public IExecutingStrategy? ExecutingStrategy { get; set; }

    /// <summary>
    /// Gets or sets whether continuations should resume on the captured
    /// <see cref="System.Threading.SynchronizationContext"/> while executing the pipeline.
    /// </summary>
    public bool ContinueOnCapturedContext { get; set; }

    /// <summary>
    /// Gets or sets additional middlewares to include in the pipelines resolved for this context,
    /// merged with the configured middlewares and executed in <see cref="AmanhecerMiddlewareOptions.Order"/>
    /// order.
    /// </summary>
    public List<AmanhecerMiddlewareOptions>? Middlewares { get; set; }

    /// <summary>
    /// Creates a shallow copy of this context: the collection properties
    /// (<see cref="TelemetryTags"/>, <see cref="Metadata"/> and <see cref="Middlewares"/>) are
    /// copied into new collections, while <see cref="Request"/>, <see cref="Response"/>,
    /// <see cref="Activity"/> and <see cref="ExecutingStrategy"/> are shared references.
    /// </summary>
    /// <remarks>
    /// Cloning is intended for the <see cref="IExecutingStrategy"/> implementations, which clone
    /// the context per resolved pipeline; it is not a general-purpose deep copy.
    /// </remarks>
    /// <returns>A shallow copy of this context.</returns>
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
            RoutingKey = RoutingKey,
            Middlewares = Middlewares == null ? null : [.. Middlewares],
            ExecutingStrategy = ExecutingStrategy
        };
    }
}
