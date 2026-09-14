using System;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// A subscription declaration for the in-memory transport.
/// </summary>
public class InMemorySubscription : Subscription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySubscription"/> class that
    /// consumes messages from the given queue, with <see cref="Subscription.MessagingSystem"/>
    /// set to <c>inmemory</c>.
    /// </summary>
    /// <param name="toRoutingKey">The routing key consumed messages are dispatched to.</param>
    /// <param name="queueName">The name of the queue messages are consumed from.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queueName"/> is null or
    /// empty.</exception>
    public InMemorySubscription(string toRoutingKey, string queueName) : base(toRoutingKey)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        QueueName = queueName;
    }

    /// <summary>
    /// Gets or sets the queue name the subscription reads from.
    /// </summary>
    /// <remarks>
    /// It should match the queue configured by the publication/provisioner pair.
    /// </remarks>
    public string QueueName { get; set; }

    /// <inheritdoc/>
    public override string MessagingSystem => "in-memory";
}
