using System;
using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

public record Defer(TimeSpan Delay) : IConsumerAction
{
    /// <summary>
    /// The shared <see cref="Defer"/> instance.
    /// </summary>
    public static Defer Instance { get; } = new Defer(TimeSpan.Zero);
}

public class DeferException : AmanhecerException
{
    public DeferException()
        : this(TimeSpan.Zero)
    {
    }

    public DeferException(TimeSpan delay)
    {
        Delay = delay;
    }

    public TimeSpan Delay { get; }
}