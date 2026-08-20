using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhecer.Abstractions;

/// <summary>
/// Carries optional caller-supplied settings for a dispatch, such as the routing key, metadata,
/// tracing activity, and the executing strategy to use.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Gets or sets the <see cref="System.Diagnostics.Activity"/> used to trace the dispatch, if any.
    /// </summary>
    Activity? Activity { get; set; }

    /// <summary>
    /// Gets or sets the additional tags to attach to the telemetry (span and metrics) recorded
    /// while processing the dispatch.
    /// </summary>
    List<KeyValuePair<string, object?>> TelemetryTags { get; set; }

    /// <summary>
    /// Gets or sets the metadata to associate with the dispatch.
    /// </summary>
    Dictionary<string, object> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the routing key used to resolve the pipeline. When <see langword="null"/>,
    /// a routing key is derived from the request.
    /// </summary>
    string? RoutingKey { get; set; }
    
    string? RequestId { get; set; }
    
    string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the strategy used to execute the resolved pipelines. When
    /// <see langword="null"/>, the default strategy is used.
    /// </summary>
    IExecutingStrategy? ExecutingStrategy { get; set; }
    
    /// <summary>
    /// Gets or sets whether continuations should resume on the captured
    /// <see cref="System.Threading.SynchronizationContext"/> while executing the dispatch.
    /// </summary>
    bool ContinueOnCapturedContext { get; set; }
}