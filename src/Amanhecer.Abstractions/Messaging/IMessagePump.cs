using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Pumps messages from an <see cref="IConsumer"/>: polls it for messages and dispatches each
/// one through the pipeline of the consumer's subscription until cancellation is requested.
/// </summary>
public interface IMessagePump
{
    /// <summary>
    /// Starts pumping messages from the consumer. The returned task completes when
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="consumer">The consumer to pump messages from.</param>
    /// <param name="cancellationToken">A token that stops the pump.</param>
    /// <returns>A <see cref="Task"/> that completes when the pump has stopped.</returns>
    Task ExecuteAsync(IConsumer consumer, CancellationToken cancellationToken = default);
}
