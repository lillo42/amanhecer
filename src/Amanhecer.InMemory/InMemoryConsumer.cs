using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// An <see cref="IConsumer"/> that reads messages from an in-memory queue and settles them
/// with channel semantics.
/// </summary>
/// <remarks>
/// Acknowledging or negatively acknowledging a message is a no-op because reading from the
/// channel already removes the message from the queue.
/// </remarks>
public class InMemoryConsumer(InMemorySubscription subscription, QueueManagement queues) : IConsumer
{
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
        if (delay != TimeSpan.Zero)
        {
            _ = Task.Delay(delay)
                .ContinueWith(async _ =>
                {
                    var channel = queues.GetChannels(subscription.QueueName);
                    await channel.Writer.WriteAsync(message);
                });
        }
        else
        {
            var channel = queues.GetChannels(subscription.QueueName);
            return channel.Writer.WriteAsync(message);
        }

        return new ValueTask();
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var channel = queues.GetChannels(subscription.QueueName);
        var message = await channel.Reader.ReadAsync(cancellationToken);
        return [message];
    }
}
