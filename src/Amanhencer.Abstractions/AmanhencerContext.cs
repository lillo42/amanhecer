using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhencer.Abstractions;

public class AmanhencerContext : IContext
{
    public Activity? Activity { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? RoutingKey { get; set; }
    public IExecutingStrategy? ExecutingStrategy { get; set; }
}
