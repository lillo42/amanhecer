using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A <see cref="IConsumerAction"/> that performs custom logic before resolving to the
/// final action used to settle the message.
/// </summary>
public interface IResolvingConsumerAction : IConsumerAction
{
    /// <summary>
    /// Executes the resolving logic for the consumed message and returns the resulting
    /// <see cref="IConsumerAction"/>.
    /// </summary>
    /// <param name="message">The consumed message being settled.</param>
    /// <param name="subscription">The subscription the message was consumed from.</param>
    /// <param name="dispatcher">The dispatcher used to post messages.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The final <see cref="IConsumerAction"/> used to settle the message.</returns>
    ValueTask<IConsumerAction> ExecuteAsync(Message message,
        ISubscription subscription, IDispatcher dispatcher,
        CancellationToken cancellationToken = default);
}