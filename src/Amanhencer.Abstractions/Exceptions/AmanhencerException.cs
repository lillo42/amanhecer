using System;

namespace Amanhencer.Abstractions.Exceptions;

public abstract class AmanhencerException : Exception
{
    protected AmanhencerException() { }
    
    protected AmanhencerException(string? message)
        : base(message)
    {
    }
    
    protected AmanhencerException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}