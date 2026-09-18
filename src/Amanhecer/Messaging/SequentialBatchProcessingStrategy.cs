using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging;

/// <summary>
/// Processes each message in a batch sequentially for a subscription.
/// </summary>
public class SequentialBatchProcessingStrategy : BatchProcessingStrategyBase, IBatchProcessingStrategy
{
    /// <summary>
    /// Processes the supplied batch one message at a time in the order received.
    /// </summary>
    /// <param name="provider">The service provider used to resolve processing dependencies.</param>
    /// <param name="subscription">The subscription associated with the consumed messages.</param>
    /// <param name="consumer">The consumer used to settle each processed message.</param>
    /// <param name="messages">The batch of messages to process.</param>
    /// <param name="cancellationToken">A token that cancels the batch processing operation.</param>
    public async ValueTask ExecuteAsync(IServiceProvider provider,
        ISubscription subscription,
        IConsumer consumer,
        Message[] messages,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        var logger = provider.GetRequiredService<ILogger<SequentialBatchProcessingStrategy>>();
        var metrics = new KeyValuePair<string, object?>[]
        {
            new("messaging.system", subscription.MessagingSystem),
            new("messaging.operation.type", "process"),
            new("messaging.destination.name", subscription.Name),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (subscription.BatchProcessingTimeout > TimeSpan.Zero)
        {
            cts.CancelAfter(subscription.BatchProcessingTimeout);
        }

        foreach (var message in messages)
        {
            await ProcessAsync(provider,
                logger,
                consumer,
                subscription,
                message,
                metrics,
                cts.Token);
        }
    }
}
