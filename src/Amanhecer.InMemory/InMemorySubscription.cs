using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// A subscription declaration for the in-memory transport.
/// </summary>
public class InMemorySubscription(string toRoutingKey) : Subscription(toRoutingKey)
{
    /// <summary>
    /// Gets or sets the queue name the subscription reads from.
    /// </summary>
    /// <remarks>
    /// It should match the queue configured by the publication/provisioner pair.
    /// </remarks>
    public string QueueName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string MessagingSystem { get; set; } = "inmemory";
}
