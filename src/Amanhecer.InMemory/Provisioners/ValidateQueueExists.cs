using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Provisioners;

/// <summary>
/// An <see cref="IPublicationProvisioner"/> and <see cref="ISubscriptionProvisioner"/> that
/// validates the configured queue exists in the gateway queue registry.
/// </summary>
public class ValidateQueueExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (gateway is not InMemoryGateway inMemoryGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(InMemoryGateway)}.",
                nameof(gateway));
        }

        if (publication is not InMemoryPublication inMemoryPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(InMemoryPublication)}.",
                nameof(publication));
        }

        if (!inMemoryGateway.Queues.Exists(inMemoryPublication.QueueName))
        {
            throw new InvalidOperationException(
                $"Queue '{inMemoryPublication.QueueName}' does not exist.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        if (gateway is not InMemoryGateway inMemoryGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(InMemoryGateway)}.",
                nameof(gateway));
        }

        if (subscription is not InMemorySubscription inMemorySubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(InMemorySubscription)}.",
                nameof(subscription));
        }

        if (!inMemoryGateway.Queues.Exists(inMemorySubscription.QueueName))
        {
            throw new InvalidOperationException(
                $"Queue '{inMemorySubscription.QueueName}' does not exist.");
        }

        return Task.CompletedTask;
    }
}
