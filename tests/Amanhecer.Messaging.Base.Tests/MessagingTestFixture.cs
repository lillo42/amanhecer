using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Base.Tests;

/// <summary>
/// Everything a test needs to exercise a transport: the
/// producer and consumer created for a fresh, isolated publication/subscription pair, plus a
/// cleanup callback that tears the transport resources (and the gateway) down.
/// </summary>
public sealed class MessagingTestFixture : IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the producer used to publish messages through <see cref="Publication"/>.
    /// </summary>
    public required IProducer Producer { get; init; }

    /// <summary>
    /// Gets or sets the publication messages are produced with.
    /// </summary>
    public required IPublication Publication { get; init; }

    /// <summary>
    /// Gets or sets the consumer receiving the messages routed to <see cref="Subscription"/>.
    /// </summary>
    public required IConsumer Consumer { get; init; }

    /// <summary>
    /// Gets or sets the subscription <see cref="Consumer"/> consumes for.
    /// </summary>
    public required ISubscription Subscription { get; init; }

    /// <summary>
    /// Gets or sets the callback that disposes the gateway and deletes the transport resources
    /// (exchanges, queues, topics, ...) created for this fixture.
    /// </summary>
    public required Func<ValueTask> Cleanup { get; init; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Cleanup();
    }
}
