using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// A publication declaration for the in-memory transport.
/// </summary>
public class InMemoryPublication : Publication
{
    /// <summary>
    /// Gets or sets the queue name the publication writes to.
    /// </summary>
    /// <remarks>
    /// When left empty, <see cref="IPublication.RoutingKey"/> is used instead.
    /// </remarks>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// The queue name every producer and provisioner of this publication resolves to, so the
    /// fallback to <see cref="IPublication.RoutingKey"/> is applied consistently regardless of
    /// which of them runs first.
    /// </summary>
    internal string ResolvedQueueName => string.IsNullOrEmpty(QueueName) ? RoutingKey : QueueName;
}
