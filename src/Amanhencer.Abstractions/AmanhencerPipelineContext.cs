using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Amanhencer.Abstractions;

/// <summary>
/// Default <see cref="IPipelineContext"/> implementation: an immutable record carrying the state
/// of a request as it flows through its pipeline.
/// </summary>
/// <param name="Activity">The <see cref="System.Diagnostics.Activity"/> used to trace the execution of the pipeline, if any.</param>
/// <param name="Metadata">The metadata associated with the request being processed.</param>
/// <param name="RoutingKey">The routing key used to resolve the pipeline for the request.</param>
/// <param name="Request">The request being processed by the pipeline.</param>
/// <param name="ExecutingStrategy">The strategy used to execute the pipelines resolved for this context.</param>
/// <param name="CancellationToken">The token that signals cancellation of the pipeline execution.</param>
public record AmanhencerPipelineContext(
    Activity? Activity,
    Dictionary<string, object> Metadata,
    string RoutingKey,
    object Request,
    IExecutingStrategy ExecutingStrategy,
    CancellationToken CancellationToken)
    : IPipelineContext
{
    /// <inheritdoc />
    public object? Response { get; set; }

    /// <summary>
    /// Creates a copy of this context with a new, shallow-copied <see cref="Metadata"/> dictionary,
    /// so changes to the clone's metadata do not affect the original context.
    /// </summary>
    /// <returns>A new <see cref="IPipelineContext"/> with the same values as this instance.</returns>
    public IPipelineContext DeepClone()
    {
        return this with
        {
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }
}