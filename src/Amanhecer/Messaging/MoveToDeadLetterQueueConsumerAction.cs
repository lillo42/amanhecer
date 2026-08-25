using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// A <see cref="IResolvingConsumerAction"/> that reposts the message to the subscription's
/// dead-letter queue routing key and then acknowledges it.
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
        await dispatcher.PostAsync(message, new AmanhecerContext
        {
            RoutingKey = subscription.DeadLetterQueueRoutingKey
        }, cancellationToken);
        return Ack.Instance;
    }
}