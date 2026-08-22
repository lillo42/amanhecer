using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="ISubscriptionProvisoner"/> that assumes the queue and its bindings already
/// exist and performs no action.
/// </summary>
public class AssumeQueueExists : ISubscriptionProvisoner
{
    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        return Task.CompletedTask;
    }
}