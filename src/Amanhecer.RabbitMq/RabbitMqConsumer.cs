using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanhecer.RabbitMq;

/// <summary>
/// An <see cref="IConsumer"/> that settles (acks, nacks or defers) RabbitMQ deliveries and
/// hands the messages buffered by the underlying <see cref="RabbitMqMessagePoller"/> to the
/// message pumper.
/// </summary>
/// <param name="poller">The poller buffering the messages received from the queue.</param>
/// <param name="subscription">The subscription this consumer consumes for.</param>
/// <param name="logger">The logger used to report settlement problems.</param>
public partial class RabbitMqConsumer(
    RabbitMqMessagePoller poller,
    RabbitMqSubscription subscription,
    ILogger<RabbitMqConsumer>? logger = null) : IConsumer
{
    private readonly ILogger<RabbitMqConsumer> _logger = logger ?? NullLogger<RabbitMqConsumer>.Instance;

    /// <inheritdoc />
    public ISubscription Subscription { get; } = subscription;

    /// <inheritdoc />
    public async ValueTask AckAsync(Message message)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await poller.AckAsync(deliveryTag);
        }
    }

    /// <inheritdoc />
    public async ValueTask NackAsync(Message message)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await poller.NackAsync(deliveryTag, requeue: false);
        }
    }

    /// <summary>
    /// Defers the message by nacking it with requeue, so the broker redelivers it.
    /// </summary>
    /// <param name="message">The message to defer.</param>
    /// <param name="delay">
    /// The delay after which the message should be redelivered. <strong>Not honored by this
    /// transport</strong>: the message is requeued immediately. Implementing a real delay
    /// requires per-message TTL with a dead-letter exchange, which is not implemented yet.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been requeued.</returns>
    public async ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await poller.NackAsync(deliveryTag, requeue: true);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var message = await poller.Messages.ReadAsync(cancellationToken);
        return [message];
    }

    private bool TryGetDeliveryTag(Message message, out ulong deliveryTag)
    {
        if (message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            && obj is ulong tag)
        {
            deliveryTag = tag;
            return true;
        }

        deliveryTag = 0;
        Logger.MissingDeliveryTag(_logger, message.Id, subscription.QueueName);
        return false;
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning,
            "Cannot settle message {MessageId} from queue {QueueName}: no delivery tag found in the message metadata")]
        public static partial void MissingDeliveryTag(ILogger logger, string messageId, string queueName);
    }
}
