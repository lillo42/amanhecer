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
    /// When left empty, the gateway falls back to <see cref="IPublication.RoutingKey"/>.
    /// </remarks>
    public string QueueName { get; set; } = null!;
}
