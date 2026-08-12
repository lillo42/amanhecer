using System;

namespace Amanhecer.Abstractions.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by Amanhecer.
/// </summary>
public abstract class AmanhecerException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhecerException"/> class.
    /// </summary>
    protected AmanhecerException() { }

    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhecerException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected AmanhecerException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="AmanhecerException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected AmanhecerException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}