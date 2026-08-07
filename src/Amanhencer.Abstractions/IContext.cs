using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhencer.Abstractions;

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
    /// Gets or sets the metadata to associate with the dispatch.
    /// </summary>
    Dictionary<string, object> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the routing key used to resolve the pipeline. When <see langword="null"/>,
    /// a routing key is derived from the request.
    /// </summary>
    string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the strategy used to execute the resolved pipelines. When
    /// <see langword="null"/>, the default strategy is used.
    /// </summary>
    IExecutingStrategy? ExecutingStrategy { get; set; }
}