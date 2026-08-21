using System.Collections.Frozen;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IProducerFinder"/> that resolves producers from a frozen dictionary keyed by
/// routing key.
/// </summary>
/// <param name="producers">The producers registered by routing key.</param>
public class AmanhecerProducerFinder(FrozenDictionary<string, IProducer> producers) : IProducerFinder
{
    /// <inheritdoc />
    public IProducer Find(string routingKey)
    {
        return producers[routingKey];
    }
}