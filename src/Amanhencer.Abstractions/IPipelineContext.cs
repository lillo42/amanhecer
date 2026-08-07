using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Amanhencer.Abstractions;

public interface IPipelineContext
{
    Activity? Activity { get; }

    Dictionary<string, object> Metadata { get; }

    CancellationToken CancellationToken { get; }

    string RoutingKey { get; }

    object Request { get; }

    object? Response { get; set; }
    
    IExecutingStrategy ExecutingStrategy { get; }

    IPipelineContext DeepClone();
}