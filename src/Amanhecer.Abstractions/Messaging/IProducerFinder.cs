namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Resolves the <see cref="IProducer"/> to use for a given routing key.
/// </summary>
public interface IProducerFinder
{
    /// <summary>
    /// Finds the producer registered for the given routing key.
    /// </summary>
    /// <param name="routingKey">The routing key of the publication to publish through.</param>
    /// <returns>The producer for the routing key.</returns>
    IProducer Find(string routingKey);
}