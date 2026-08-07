namespace Amanhencer.Abstractions.Exceptions;

public class PipelineNotFoundException(string routingKey)
    : AmanhencerException("Pipeline not found for routingKey: " + routingKey)
{
    public string RoutingKey { get; } = routingKey;
}