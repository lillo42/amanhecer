namespace Amanhencer.Abstractions.Exceptions;

/// <summary>
/// The exception thrown when no pipeline is found for a routing key during a <c>Send</c> or
/// <c>Query</c> dispatch, which require exactly one pipeline.
/// </summary>
/// <param name="routingKey">The routing key for which no pipeline was found.</param>
public class PipelineNotFoundException(string routingKey)
    : AmanhencerException("Pipeline not found for routingKey: " + routingKey)
{
    /// <summary>
    /// Gets the routing key for which no pipeline was found.
    /// </summary>
    public string RoutingKey { get; } = routingKey;
}