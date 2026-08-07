namespace Amanhencer.Abstractions.Exceptions;

/// <summary>
/// The exception thrown when more than one pipeline is found for a routing key during a
/// <c>Send</c> or <c>Query</c> dispatch, which require exactly one pipeline.
/// </summary>
/// <param name="routingKey">The routing key that matched multiple pipelines.</param>
public class MultiPipelineFoundException(string routingKey)
    : AmanhencerException("Multi-pipeline found for routingKey: " + routingKey)
{
    /// <summary>
    /// Gets the routing key that matched multiple pipelines.
    /// </summary>
    public string RoutingKey { get; } = routingKey;
}