namespace Amanhencer.Abstractions.Exceptions;

public class MultiPipelineFoundException(string routingKey)
    : AmanhencerException("Multi-pipeline found for routingKey: " + routingKey)
{
    public string RoutingKey { get; } = routingKey;
}