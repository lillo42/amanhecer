using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

public record Defer : IConsumerAction
{
    /// <summary>
    /// The shared <see cref="Defer"/> instance.
    /// </summary>
    public static Defer Instance { get; } = new Defer();
}

public class DeferException : AmanhecerException;