using System.Collections.Frozen;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IPublicationFinder"/> that resolves publications from a frozen dictionary keyed by
/// routing key.
/// </summary>
/// <param name="publications">The publications registered by routing key.</param>
public class AmanhecerPublicationFinder(FrozenDictionary<string, IPublication> publications) : IPublicationFinder
{
    /// <inheritdoc />
    public IPublication Find(string routingKey)
    {
        return publications[routingKey];
    }
}