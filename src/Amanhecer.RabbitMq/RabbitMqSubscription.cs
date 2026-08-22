using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A subscription that consumes messages from a RabbitMQ queue.
/// </summary>
public class RabbitMqSubscription : Subscription 
{
    /// <summary>
    /// Gets or sets the name of the queue messages are consumed from.
    /// </summary>
    public required string QueueName { get; set; }
}