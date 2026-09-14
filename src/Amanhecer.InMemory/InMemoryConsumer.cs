using System;
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
public partial class InMemoryConsumer : IConsumer
{
    private readonly InMemorySubscription _subscription;
    private readonly QueueManagement _queues;
    private readonly ILogger<InMemoryConsumer> _logger;

    // The token the message pump waits on. Captured so a delayed requeue stops with the host
    // instead of firing after shutdown.
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryConsumer"/> class.
    /// </summary>
    /// <param name="subscription">The subscription this consumer consumes for.</param>
    /// <param name="queues">The queue registry the subscription's queue is read from.</param>
    /// <param name="logger">The logger used to report settlement problems.</param>
    public InMemoryConsumer(InMemorySubscription subscription,
        QueueManagement queues,
        ILogger<InMemoryConsumer>? logger = null)
    {
        _subscription = subscription;
        _queues = queues;
        _logger = logger ?? NullLogger<InMemoryConsumer>.Instance;
    }

    /// <inheritdoc />
    public ISubscription Subscription => _subscription;

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
            return _queues.GetChannels(_subscription.QueueName)
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
                await _queues.GetChannels(_subscription.QueueName)
                    .Writer
                    .WriteAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The host is shutting down: the message is dropped with the rest of the queue.
            }
            catch (Exception e)
            {
                Logger.RequeueFailed(_logger, e, message.Id, _subscription.QueueName);
            }
        }, cancellationToken);

        return new ValueTask();
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        var channel = _queues.GetChannels(_subscription.QueueName);
        var message = await channel.Reader.ReadAsync(cancellationToken);
        return [message];
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Error,
            "Failed to requeue the deferred message {MessageId} on queue {QueueName}: the message is lost")]
        public static partial void RequeueFailed(ILogger logger, Exception exception, string messageId, string queueName);
    }
}
