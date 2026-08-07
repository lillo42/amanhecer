using System.Collections.Generic;
using System.Diagnostics;

namespace Amanhencer.Abstractions;

public interface IContext
{
    Activity? Activity { get; set; }
    Dictionary<string, object> Metadata { get; set; }
    string? RoutingKey { get; set; }
    IExecutingStrategy? ExecutingStrategy { get; set; }
}