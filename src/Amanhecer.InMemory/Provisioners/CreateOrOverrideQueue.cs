using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Provisioners;

/// <summary>
/// An <see cref="IPublicationProvisioner"/> and <see cref="ISubscriptionProvisioner"/> that
/// creates or replaces the in-memory queue channel used by a publication or subscription.
/// </summary>
public class CreateOrOverrideQueue : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <summary>
    /// Gets or sets the queue capacity.
    /// </summary>
    /// <remarks>
    /// Values less than or equal to <c>0</c> create an unbounded channel.
    /// </remarks>
    public int Capacity { get; set; } = -1;

    /// <summary>
    /// Gets or sets the behavior applied when a bounded channel is full.
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

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

        var queueName = string.IsNullOrEmpty(inMemoryPublication.QueueName)
            ? inMemoryPublication.RoutingKey
            : inMemoryPublication.QueueName;

        CreateQueue(inMemoryGateway, queueName);
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

        CreateQueue(inMemoryGateway, inMemorySubscription.QueueName);
        return Task.CompletedTask;
    }

    private void CreateQueue(InMemoryGateway gateway, string queueName)
    {
        if (Capacity <= 0)
        {
            gateway.Queues.AddChannel(queueName,
                Channel.CreateUnbounded<Message>(new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                }));
        }
        else
        {
            gateway.Queues.AddChannel(queueName,
                Channel.CreateBounded<Message>(new BoundedChannelOptions(Capacity)
                {
                    FullMode = FullMode,
                    AllowSynchronousContinuations = false,
                    SingleReader = false,
                    SingleWriter = false,
                }));
        }
    }
}
