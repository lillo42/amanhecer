using System.Collections.Frozen;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

public class AmanhecerPublicationFinder(FrozenDictionary<string, IPublication> publications) : IPublicationFinder
{
    public IPublication Find(string routingKey)
    {
        return publications[routingKey];
    }
}