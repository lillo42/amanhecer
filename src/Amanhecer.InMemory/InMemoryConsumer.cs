using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanhecer.InMemory;

/// <summary>
/// An <see cref="IConsumer"/> that reads messages from an in-memory queue and settles them
/// with channel semantics.
/// </summary>
/// <remarks>
/// Acknowledging or negatively acknowledging a message is a no-op because reading from the
/// channel already removes the message from the queue.
/// </remarks>
public partial class InMemoryConsumer(
    InMemorySubscription subscription,
    QueueManagement queues,
    ILogger<InMemoryConsumer>? logger = null) : IConsumer
{
    private readonly ILogger<InMemoryConsumer> _logger = logger ?? NullLogger<InMemoryConsumer>.Instance;

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
            var channel = queues.GetChannel(subscription.QueueName);
            return channel.Writer.WriteAsync(message);
        }

        // The delayed requeue is scheduled instead of awaited so the pump is not blocked for
        // the delay. Failures must be logged: reading already removed the only copy of the
        // message from the queue, so a failed requeue means the message is lost.
        _ = RequeueAfterDelay(message, delay);
        return new ValueTask();
    }

    private async Task RequeueAfterDelay(Message message, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay).ConfigureAwait(false);
            var channel = queues.GetChannel(subscription.QueueName);
            await channel.Writer.WriteAsync(message).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.DeferredMessageLost(_logger, e, message.Id, subscription.QueueName);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var channel = queues.GetChannel(subscription.QueueName);
        var message = await channel.Reader.ReadAsync(cancellationToken);

        if (subscription.BufferSize <= 1)
        {
            return [message];
        }

        var messages = new List<Message>(subscription.BufferSize) { message };
        while (messages.Count < subscription.BufferSize && channel.Reader.TryRead(out var next))
        {
            messages.Add(next);
        }

        return [.. messages];
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Error,
            "Deferred message {MessageId} could not be requeued to queue {QueueName} and was lost")]
        public static partial void DeferredMessageLost(ILogger logger, Exception exception, string messageId, string queueName);
    }
}
