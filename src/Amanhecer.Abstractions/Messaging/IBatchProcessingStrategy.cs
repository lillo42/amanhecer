using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Defines how a batch of consumed messages is processed for a subscription.
/// </summary>
public interface IBatchProcessingStrategy
{
    /// <summary>
    /// Processes the supplied batch of messages for the given subscription.
    /// </summary>
    /// <param name="provider">The service provider used to resolve processing dependencies.</param>
    /// <param name="subscription">The subscription associated with the consumed messages.</param>
    /// <param name="consumer">The consumer used to settle each processed message.</param>
    /// <param name="messages">The batch of messages to process.</param>
    /// <param name="cancellationToken">A token that cancels the batch processing operation.</param>
    ValueTask ExecuteAsync(IServiceProvider provider,
        ISubscription subscription,
        IConsumer consumer,
        Message[] messages,
        CancellationToken cancellationToken);
}
