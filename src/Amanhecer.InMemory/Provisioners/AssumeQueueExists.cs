using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Provisioners;

/// <summary>
/// An <see cref="IPublicationProvisioner"/> and <see cref="ISubscriptionProvisioner"/> that
/// assumes queues already exist and performs no action.
/// </summary>
public class AssumeQueueExists : IPublicationProvisioner, ISubscriptionProvisioner
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
