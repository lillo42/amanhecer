using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A negative acknowledgement returned by a consumer handler to signal that the message
/// could not be processed.
/// </summary>
/// <param name="Requeue">Whether the message should be requeued by the transport instead
/// of being discarded or dead-lettered.</param>
public record Nack(bool Requeue);

/// <summary>
/// The exception thrown to signal a negative acknowledgement (<see cref="Nack"/>) for the
/// message being handled.
/// </summary>
/// <param name="requeue">Whether the message should be requeued by the transport.</param>
public class NackException(bool requeue) : AmanhecerException
{
    /// <summary>
    /// Gets or sets whether the message should be requeued by the transport instead of
    /// being discarded or dead-lettered.
    /// </summary>
    public bool Requeue { get; set; } = requeue;
}