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
public class InMemoryGateway : Gateway<InMemoryPublication, InMemorySubscription>, ILoggerFactorySupport
{
    private readonly SemaphoreSlim _provisioningLock = new(1, 1);
    private bool _provisioned;

    /// <summary>
    /// Gets the queue registry used by producers, consumers and provisioners of this gateway.
    /// </summary>
    public QueueManagement Queues { get; } = new();

    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Executes the provisioners declared by the publications and subscriptions. Provisioning
    /// runs at most once per gateway: later calls are no-ops, unless the first attempt failed,
    /// in which case the next call retries.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
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
        var seen = new HashSet<string>();
        foreach (var publication in Publications)
        {
            if (!seen.Add(publication.RoutingKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate publication routing key '{publication.RoutingKey}': two publications are registered with the same routing key.");
            }
        }

        // The publish path provisions lazily: the first producer creation runs the
        // provisioners, so publish-only applications get their queues before the first publish.
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

        return new InMemoryConsumer(inMemorySubscription, Queues, LoggerFactory?.CreateLogger<InMemoryConsumer>());
    }
}
