using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Provisioners;

/// <summary>
/// A provisioner that assumes the super stream of a publication or subscription already exists
/// on the broker and performs no provisioning.
/// </summary>
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
