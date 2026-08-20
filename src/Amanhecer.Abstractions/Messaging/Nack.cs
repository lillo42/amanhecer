using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

public record Nack(bool Requeue);

public class NackException(bool requeue) : AmanhecerException
{
    public bool Requeue { get; set; }
}