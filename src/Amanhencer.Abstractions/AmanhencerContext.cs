using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhencer.Abstractions;

/// <summary>
/// Default <see cref="IContext"/> implementation used when dispatching requests through
/// <see cref="IProcessor"/>.
/// </summary>
public class AmanhencerContext : IContext
{
    /// <inheritdoc />
    public Activity? Activity { get; set; }

    /// <inheritdoc />
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <inheritdoc />
    public string? RoutingKey { get; set; }

    /// <inheritdoc />
    public IExecutingStrategy? ExecutingStrategy { get; set; }
}
