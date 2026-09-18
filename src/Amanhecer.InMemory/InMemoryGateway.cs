using System;
using System.Collections.Generic;
using System.Linq;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.InMemory;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, in-memory queues backed
/// by <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
public class InMemoryGateway : Gateway<InMemoryPublication, InMemorySubscription>, ILoggerFactorySupport
{
    /// <summary>
    /// Gets the queue registry used by producers, consumers and provisioners of this gateway.
    /// </summary>
    public QueueManagement Queues { get; } = new();

    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        return Publications
            .ToDictionary(x => x.RoutingKey, IProducer (x) => new InMemoryProducer(Queues));
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

        return new InMemoryConsumer(inMemorySubscription, Queues, LoggerFactory?.CreateLogger<InMemoryConsumer>());
    }
}
