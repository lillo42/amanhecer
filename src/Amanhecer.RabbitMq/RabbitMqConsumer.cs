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
/// message pump.
/// </summary>
public partial class RabbitMqConsumer : IConsumer
{
    private readonly RabbitMqMessagePoller _poller;
    private readonly RabbitMqSubscription _subscription;
    private readonly ILogger<RabbitMqConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqConsumer"/> class.
    /// </summary>
    /// <param name="poller">The poller buffering the messages received from the queue.</param>
    /// <param name="subscription">The subscription this consumer consumes for.</param>
    /// <param name="logger">The logger used to report settlement problems.</param>
    public RabbitMqConsumer(RabbitMqMessagePoller poller,
        RabbitMqSubscription subscription,
        ILogger<RabbitMqConsumer>? logger = null)
    {
        _poller = poller;
        _subscription = subscription;
        Subscription = subscription;
        _logger = logger ?? NullLogger<RabbitMqConsumer>.Instance;

        if (subscription.MaxDeliveryAttempts <= 0)
        {
            Logger.NoMaxDeliveryAttemptsConfigured(_logger, subscription.QueueName);
        }
    }

    /// <inheritdoc />
    public ISubscription Subscription { get; }

    /// <inheritdoc />
    public async ValueTask AckAsync(Message message)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await _poller.AckAsync(deliveryTag);
            _poller.ClearDeliveryAttempts(message.Id);
        }
    }

    /// <inheritdoc />
    public async ValueTask NackAsync(Message message)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await _poller.NackAsync(deliveryTag, requeue: false);
            _poller.ClearDeliveryAttempts(message.Id);
        }
    }

    /// <summary>
    /// Defers the message by nacking it with requeue, so the broker redelivers it. When
    /// <see cref="RabbitMqSubscription.MaxDeliveryAttempts"/> is greater than <c>0</c> and the
    /// message has been delivered that many times, it is nacked without requeue instead, so
    /// the broker dead-letters it (when a dead-letter exchange is configured) rather than
    /// redelivering it forever. When <see cref="RabbitMqSubscription.MaxDeliveryAttempts"/>
    /// is <c>0</c> (the default), the client-side cap is disabled and the message is always
    /// requeued, deferring to the broker's redelivery policy.
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
        if (!TryGetDeliveryTag(message, out var deliveryTag))
        {
            return;
        }

        var attempts = message.Metadata.TryGetValue(MetadataName.DeliveryAttempts, out var value)
                       && value is int count
            ? count
            : 1;

        if (_subscription.MaxDeliveryAttempts > 0 && attempts >= _subscription.MaxDeliveryAttempts)
        {
            Logger.MaxDeliveryAttemptsReached(_logger,
                message.Id,
                _subscription.QueueName,
                _subscription.MaxDeliveryAttempts);
            await _poller.NackAsync(deliveryTag, requeue: false);
            _poller.ClearDeliveryAttempts(message.Id);
            return;
        }

        await _poller.NackAsync(deliveryTag, requeue: true);
    }

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        await _poller.Messages.WaitToReadAsync(cancellationToken);

        var buffer = new Message[_subscription.BufferSize];
        for (var i = 0; i < _subscription.BufferSize && !cancellationToken.IsCancellationRequested; i++)
        {
            if (!_poller.Messages.TryRead(out var message))
            {
                break;
            }

            buffer[i] = message;
        }

        return buffer;
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
        Logger.MissingDeliveryTag(_logger, message.Id, _subscription.QueueName);
        return false;
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning,
            "Cannot settle message {MessageId} from queue {QueueName}: no delivery tag found in the message metadata")]
        public static partial void MissingDeliveryTag(ILogger logger, string messageId, string queueName);

        [LoggerMessage(LogLevel.Warning,
            "Message {MessageId} from queue {QueueName} reached the maximum of {MaxDeliveryAttempts} delivery attempts: nacking without requeue")]
        public static partial void MaxDeliveryAttemptsReached(ILogger logger, string messageId, string queueName,
            int maxDeliveryAttempts);

        [LoggerMessage(LogLevel.Warning,
            "No maximum delivery attempts configured for queue {QueueName}: a persistently failing message is requeued and redelivered immediately, forever. Configure a broker-side redelivery policy (e.g. a quorum queue delivery-limit with a dead-letter exchange) or set MaxDeliveryAttempts on the subscription to bound redeliveries.")]
        public static partial void NoMaxDeliveryAttemptsConfigured(ILogger logger, string queueName);
    }
}