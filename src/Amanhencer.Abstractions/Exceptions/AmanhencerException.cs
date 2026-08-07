using System;

namespace Amanhencer.Abstractions.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by Amanhencer.
/// </summary>
public abstract class AmanhencerException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhencerException"/> class.
    /// </summary>
    protected AmanhencerException() { }

    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhencerException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected AmanhencerException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhencerException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected AmanhencerException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}