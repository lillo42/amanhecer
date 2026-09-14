using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.InMemory;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, in-memory queues backed
/// by <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
public class InMemoryGateway : Gateway<InMemoryPublication, InMemorySubscription>
    , ILoggerFactorySupport
{
    private readonly SemaphoreSlim _provisioningLock = new(1, 1);
    private bool _provisioned;

    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets the queue registry used by producers, consumers and provisioners of this gateway.
    /// </summary>
    public QueueManagement Queues { get; } = new();

    /// <inheritdoc />
    /// <remarks>
    /// Provisioning runs at most once: the queues are the state of this transport, so
    /// re-provisioning would replace the channels and discard the messages still queued in them.
    /// </remarks>
    public override async ValueTask ProvisionerAsync()
    {
        if (_provisioned)
        {
            return;
        }

        await _provisioningLock.WaitAsync();
        try
        {
            if (_provisioned)
            {
                return;
            }

            await base.ProvisionerAsync();

            _provisioned = true;
        }
        finally
        {
            _provisioningLock.Release();
        }
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        // The publish path provisions lazily: the first producer creation declares the
        // queues, so publish-only applications get their topology before the first publish.
        ProvisionerAsync().GetAwaiter().GetResult();

        var producers = new Dictionary<string, IProducer>();

        foreach (var publication in Publications)
        {
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

        return new InMemoryConsumer(inMemorySubscription,
            Queues,
            LoggerFactory?.CreateLogger<InMemoryConsumer>());
    }
}
