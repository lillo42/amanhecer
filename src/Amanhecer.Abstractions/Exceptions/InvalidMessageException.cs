using System;

namespace Amanhecer.Abstractions.Exceptions;

/// <summary>
/// The exception thrown when a consumed message cannot be mapped to the request type
/// expected by the pipeline handling it.
/// </summary>
public class InvalidMessageException : AmanhecerException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidMessageException"/> class.
    /// </summary>
    /// <param name="inner">The exception that caused the mapping failure, if any.</param>
    public InvalidMessageException(Exception? inner = null)
        : base("The message could not be mapped to the expected request type.", inner)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidMessageException"/> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that caused the mapping failure, if any.</param>
    public InvalidMessageException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
