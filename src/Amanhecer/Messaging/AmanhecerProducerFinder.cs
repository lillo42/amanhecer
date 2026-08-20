using System.Collections.Frozen;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

public class AmanhecerProducerFinder(FrozenDictionary<string, IProducer> producers) : IProducerFinder
{
    public IProducer Find(string routingKey)
    {
        return producers[routingKey];
    }
}