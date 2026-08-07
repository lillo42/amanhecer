using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Amanhencer.Abstractions;

public record AmanhencerPipelineContext(
    Activity? Activity,
    Dictionary<string, object> Metadata,
    string RoutingKey,
    object Request,
    IExecutingStrategy ExecutingStrategy,
    CancellationToken CancellationToken)
    : IPipelineContext
{
    public object? Response { get; set; }
    
    public IPipelineContext DeepClone()
    {
        return this with
        {
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }
}