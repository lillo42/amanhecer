using System.Collections.Generic;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IGateway
{
    string Name { get; }

    IEnumerable<IPublication> Publications { get; }
    IEnumerable<ISubscription> Subscriptions { get; }


    ValueTask ProvisionerAsync();
    IReadOnlyDictionary<string, IProducer> CreateProducers();
    IEnumerable<IConsumer> CreateSubscriptions();
}