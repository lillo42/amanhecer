using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A <see cref="IResolvingConsumerAction"/> that reposts the message to the subscription's
/// dead-letter queue routing key and then acknowledges it. When the subscription has no
/// <see cref="ISubscription.DeadLetterQueueRoutingKey"/> configured, the message cannot be
/// moved and the action degrades to a <see cref="Defer"/>.
/// </summary>
public class MoveToDeadLetterQueueConsumerAction : IResolvingConsumerAction
{
    /// <summary>
    /// The shared <see cref="MoveToDeadLetterQueueConsumerAction"/> instance.
    /// </summary>
    public static MoveToDeadLetterQueueConsumerAction Instance { get; } = new();

    /// <inheritdoc />
    public async ValueTask<IConsumerAction> ExecuteAsync(Message message,
        ISubscription subscription,
        IDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        if (subscription.DeadLetterQueueRoutingKey == null)
        {
            return Defer.Instance;
        }

        await dispatcher.PostAsync(message, new AmanhecerContext
        {
            RoutingKey = subscription.DeadLetterQueueRoutingKey
        }, cancellationToken);

        return Ack.Instance;
    }
}
