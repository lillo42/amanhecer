using System;
using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A consumer action that settles the message by requeueing it, so it is delivered again
/// after the given delay.
/// </summary>
/// <param name="Delay">The delay after which the message should be redelivered.</param>
public record Defer(TimeSpan Delay) : IConsumerAction
{
    /// <summary>
    /// The shared <see cref="Defer"/> instance, with a zero delay.
    /// </summary>
    public static Defer Instance { get; } = new Defer(TimeSpan.Zero);
}

/// <summary>
/// The exception thrown to signal that the message being handled should be deferred
/// (see <see cref="Defer"/>).
/// </summary>
public class DeferException : AmanhecerException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DeferException"/> class with a zero delay.
    /// </summary>
    public DeferException()
        : this(TimeSpan.Zero)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="DeferException"/> class with the given delay.
    /// </summary>
    /// <param name="delay">The delay after which the message should be redelivered.</param>
    public DeferException(TimeSpan delay)
    {
        Delay = delay;
    }

    /// <summary>
    /// Gets the delay after which the message should be redelivered.
    /// </summary>
    public TimeSpan Delay { get; }
}
