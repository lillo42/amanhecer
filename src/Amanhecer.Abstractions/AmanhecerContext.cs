using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhecer.Abstractions;

/// <summary>
/// Default <see cref="IContext"/> implementation used when dispatching requests through
/// <see cref="IDispatcher"/>.
/// </summary>
public class AmanhecerContext : IContext
{
    /// <inheritdoc />
    public Activity? Activity { get; set; }

    /// <inheritdoc />
    public List<KeyValuePair<string, object?>> TelemetryTags { get; set; } = [];

    /// <inheritdoc />
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <inheritdoc />
    public string? RoutingKey { get; set; }

    /// <inheritdoc />
    public IExecutingStrategy? ExecutingStrategy { get; set; }

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }
}
