namespace Amanhecer.Abstractions.Messaging;

public interface IProducerFinder
{
    IProducer Find(string routingKey);
}