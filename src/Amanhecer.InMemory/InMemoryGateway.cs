using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, in-memory queues backed
/// by <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
public class InMemoryGateway : Gateway<InMemoryPublication, InMemorySubscription>
{
    /// <summary>
    /// Gets the queue registry used by producers, consumers and provisioners of this gateway.
    /// </summary>
    public QueueManagement Queues { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        var producers = new Dictionary<string, IProducer>();

        foreach (var publication in Publications)
        {
            if (string.IsNullOrEmpty(publication.QueueName))
            {
                publication.QueueName = publication.RoutingKey;
            }

            producers.Add(publication.RoutingKey, new InMemoryProducer(Queues));
        }

        return producers;
    }

    /// <inheritdoc />
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not InMemorySubscription inMemorySubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(InMemorySubscription)}.",
                nameof(subscription));
        }

        return new InMemoryConsumer(inMemorySubscription, Queues);
    }
}
