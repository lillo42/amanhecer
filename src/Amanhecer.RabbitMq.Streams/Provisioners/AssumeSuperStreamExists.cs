using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Provisioners;

public class AssumeSuperStreamExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        return Task.CompletedTask;
    }
}
