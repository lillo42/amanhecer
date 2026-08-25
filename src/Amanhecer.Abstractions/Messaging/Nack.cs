using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A negative acknowledgement returned by a consumer handler to signal that the message
/// could not be processed.
/// </summary>
public record Nack : IConsumerAction
{
    /// <summary>
    /// The shared <see cref="Nack"/> instance.
    /// </summary>
    public static Nack Instance { get; } = new();
}

/// <summary>
/// The exception thrown to signal a negative acknowledgement (<see cref="Nack"/>) for the
/// message being handled.
/// </summary>
public class NackException() : AmanhecerException;