using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The runtime actor that receives messages from the transport for a given subscription and
/// settles them (ack, nack or defer) once they have been processed.
/// </summary>
public interface IConsumer
{
    /// <summary>
    /// Gets the subscription this consumer consumes messages for.
    /// </summary>
    ISubscription Subscription { get; }

    /// <summary>
    /// Acknowledges the message, removing it from the transport.
    /// </summary>
    /// <param name="message">The message to acknowledge.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been acknowledged.</returns>
    ValueTask AckAsync(Message message);

    /// <summary>
    /// Negatively acknowledges the message, so the transport drops it or routes it to its
    /// dead-letter destination.
    /// </summary>
    /// <param name="message">The message to negatively acknowledge.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been settled.</returns>
    ValueTask NackAsync(Message message);

    /// <summary>
    /// Defers the message, requeueing it so it is delivered again after the given delay.
    /// </summary>
    /// <param name="message">The message to defer.</param>
    /// <param name="delay">The delay after which the message should be redelivered.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been requeued.</returns>
    ValueTask DeferAsync(Message message, TimeSpan delay);

    /// <summary>
    /// Gets the next batch of messages received from the transport.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for messages.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the received messages.</returns>
    ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default);
}
