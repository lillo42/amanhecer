using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A subscription that consumes messages from a RabbitMQ queue.
/// </summary>
public class RabbitMqSubscription : Subscription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqSubscription"/> class that
    /// consumes messages from the given queue, with <see cref="Subscription.MessagingSystem"/>
    /// set to <c>rabbitmq</c>.
    /// </summary>
    /// <param name="toRoutingKey">The routing key consumed messages are dispatched to.</param>
    /// <param name="queueName">The name of the queue messages are consumed from.</param>
    public RabbitMqSubscription(string toRoutingKey, string queueName) : base(toRoutingKey)
    {
        QueueName = queueName;
        MessagingSystem = "rabbitmq";
    }

    /// <summary>
    /// Gets or sets the name of the queue messages are consumed from.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the prefetch size (the QoS window) in bytes. Defaults to <c>0</c>,
    /// meaning no limit. Note that RabbitMQ brokers ignore the prefetch size; use
    /// <see cref="Subscription.BufferSize"/> to limit how many messages each consumer
    /// prefetches.
    /// </summary>
    public uint PrefetchSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of times a message is delivered before a deferred
    /// message is nacked without requeue, so the broker dead-letters it (when a dead-letter
    /// exchange is configured) instead of redelivering it forever. Defaults to <c>0</c>
    /// (disabled): deferred messages are always requeued, deferring to the broker's
    /// redelivery policy (e.g. a quorum queue's <c>delivery-limit</c> with a dead-letter
    /// exchange), and a warning is logged when the consumer starts, because a persistently
    /// failing handler otherwise spins a hot receive/fail/requeue loop. Set a positive
    /// value to bound redeliveries client-side; with both configured, whichever limit is
    /// reached first dead-letters the message.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; }
}
