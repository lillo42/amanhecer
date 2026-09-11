using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The runtime actor that publishes a <see cref="Message"/> to the transport
/// for a given publication.
/// </summary>
public interface IProducer
{
    /// <summary>
    /// Publishes a message to the transport.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="publication">The publication the message is published through; carries
    /// the routing key and the default CloudEvents attributes to apply.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been published.</returns>
    ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context);
}