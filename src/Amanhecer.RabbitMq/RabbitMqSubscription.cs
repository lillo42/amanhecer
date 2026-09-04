using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A subscription that consumes messages from a RabbitMQ queue.
/// </summary>
public class RabbitMqSubscription(string toRoutingKey, string queueName) : Subscription(toRoutingKey)
{
    /// <summary>
    /// Gets or sets the name of the queue messages are consumed from.
    /// </summary>
    public string QueueName { get; set; } = queueName;

    /// <summary>
    /// Gets or sets the prefetch size (the QoS window) in bytes. Defaults to <c>0</c>,
    /// meaning no limit. Note that RabbitMQ brokers ignore the prefetch size; use
    /// <see cref="Subscription.BufferSize"/> to limit how many messages each consumer
    /// prefetches.
    /// </summary>
    public uint PrefetchSize { get; set; }
}