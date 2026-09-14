using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.InMemory;

/// <summary>
/// An <see cref="IConsumer"/> that reads messages from an in-memory queue and settles them
/// with channel semantics.
/// </summary>
/// <remarks>
/// Acknowledging or negatively acknowledging a message is a no-op because reading from the
/// channel already removes the message from the queue.
/// </remarks>
public class InMemoryConsumer(
    InMemorySubscription subscription,
    QueueManagement queues,
    ILogger<InMemoryConsumer>? logger = null) : IConsumer
{
    // The token the message pump waits on. Captured so a delayed requeue stops with the host
    // instead of firing after shutdown.
    private CancellationToken _cancellationToken;

    /// <inheritdoc />
    public ISubscription Subscription => subscription;

    /// <inheritdoc />
    public ValueTask AckAsync(Message message)
    {
        return new ValueTask();
    }

    /// <inheritdoc />
    public ValueTask NackAsync(Message message)
    {
        return new ValueTask();
    }

    /// <inheritdoc />
    public ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (delay == TimeSpan.Zero)
        {
            return queues.GetChannels(subscription.QueueName)
                .Writer
                .WriteAsync(message, _cancellationToken);
        }

        var cancellationToken = _cancellationToken;

        // Task.Run (not ContinueWith) so the requeue is a single observed task: a failure is
        // logged here instead of surfacing as an unobserved exception at collection time.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                await queues.GetChannels(subscription.QueueName)
                    .Writer
                    .WriteAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The host is shutting down: the message is dropped with the rest of the queue.
            }
            catch (Exception e)
            {
                logger?.LogError(e,
                    "Failed to requeue the deferred message {MessageId} on queue {QueueName}; the message is lost",
                    message.Id,
                    subscription.QueueName);
            }
        }, cancellationToken);

        return new ValueTask();
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        var channel = queues.GetChannels(subscription.QueueName);
        var message = await channel.Reader.ReadAsync(cancellationToken);
        return [message];
    }
}
