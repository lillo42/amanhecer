using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public abstract class Gateway<TPublication, TSubscription> : IGateway
    where TPublication : IPublication
    where TSubscription : ISubscription
{
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    public List<TPublication> Publications { get; set; }
    public List<TSubscription> Subscriptions { get; set; }


    IEnumerable<IPublication> IGateway.Publications => Publications.Cast<IPublication>();

    IEnumerable<ISubscription> IGateway.Subscriptions => Subscriptions.Cast<ISubscription>();

    public virtual async ValueTask ProvisionerAsync()
    {
        foreach (var publication in Publications)
        {
            if (publication.Provisioner != null)
            {
                await publication
                    .Provisioner
                    .ExecuteAsync(this, publication);
            }
        }

        foreach (var subscription in Subscriptions)
        {
            if (subscription.Provisioner != null)
            {
                await subscription
                    .Provisioner
                    .ExecuteAsync(this, subscription);
            }
        }
    }

    public abstract IReadOnlyDictionary<string, IProducer> CreateProducers();
    public abstract IEnumerable<IConsumer> CreateSubscriptions();
}