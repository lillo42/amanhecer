using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging;

/// <summary>
/// The default <see cref="IMessagePump"/>: polls an <see cref="IConsumer"/> for messages,
/// dispatches each one through the pipeline of the consumer's subscription and settles it
/// (ack, nack or defer) according to the result, until cancellation is requested.
/// </summary>
/// <param name="provider">The service provider used to create a scope per received batch.</param>
/// <param name="logger">The logger used to record pump failures.</param>
public partial class AmanhecerMessagePump(IServiceProvider provider, ILogger<AmanhecerMessagePump> logger) : IMessagePump
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(IConsumer consumer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var subscription = consumer.Subscription;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (subscription.ReceiveMessageTimeout != Timeout.InfiniteTimeSpan)
                {
                    cts.CancelAfter(subscription.ReceiveMessageTimeout);
                }

                var messages = await ReceiveMessagesAsync(consumer, cts.Token);
                if (messages.Length == 0 && subscription.NoMessageDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.NoMessageDelay, cancellationToken);
                }

                var batchProcessingStrategy = subscription.BatchProcessingStrategy ?? new SequentialBatchProcessingStrategy();
                await batchProcessingStrategy
                    .ExecuteAsync(provider, subscription, consumer, messages, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (subscription.NoMessageDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.NoMessageDelay, cancellationToken);
                }

                break;
            }
            catch (Exception e)
            {
                Logger.PumpFailed(logger, subscription.Name, e);

                if (subscription.FailureDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.FailureDelay, cancellationToken);
                }
            }
        }
    }

    private static async ValueTask<Message[]> ReceiveMessagesAsync(IConsumer consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await consumer.GetMessagesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Error,
            "An error occurred while receiving messages from subscription {SubscriptionName}; retrying after the failure delay.")]
        public static partial void PumpFailed(ILogger logger, string subscriptionName, Exception exception);

        [LoggerMessage(LogLevel.Error,
            "The error action of subscription {SubscriptionName} failed; the message is nacked instead.")]
        public static partial void ErrorActionFailed(ILogger logger, string subscriptionName, Exception exception);
    }
}