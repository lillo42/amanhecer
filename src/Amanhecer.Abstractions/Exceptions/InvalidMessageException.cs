using System;

namespace Amanhecer.Abstractions.Exceptions;

public class InvalidMessageException(Exception? inner) : AmanhecerException("error during mapping the message", inner)
{
    
}