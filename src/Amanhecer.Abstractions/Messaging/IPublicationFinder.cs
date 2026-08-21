namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Resolves the <see cref="IPublication"/> declared for a given routing key.
/// </summary>
public interface IPublicationFinder
{
    /// <summary>
    /// Finds the publication registered for the given routing key.
    /// </summary>
    /// <param name="routingKey">The routing key the publication was declared with.</param>
    /// <returns>The publication for the routing key.</returns>
    IPublication Find(string routingKey);
}