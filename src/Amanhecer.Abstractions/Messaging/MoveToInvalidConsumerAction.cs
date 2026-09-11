using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A <see cref="IResolvingConsumerAction"/> that reposts the message to the subscription's
/// invalid-message routing key and then acknowledges it. When the subscription has no
/// <see cref="ISubscription.InvalidMessageRoutingKey"/> configured, the message cannot be
/// moved and the action degrades to a <see cref="Nack"/>.
/// </summary>
public class MoveToInvalidConsumerAction : IResolvingConsumerAction
{
    /// <summary>
    /// The shared <see cref="MoveToInvalidConsumerAction"/> instance.
    /// </summary>
    public static MoveToInvalidConsumerAction Instance { get; } = new();

    /// <inheritdoc />
    public async ValueTask<IConsumerAction> ExecuteAsync(Message message,
        ISubscription subscription,
        IDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        if (subscription.InvalidMessageRoutingKey == null)
        {
            return Nack.Instance;
        }

        await dispatcher.PostAsync(message, new AmanhecerContext
        {
            RoutingKey = subscription.InvalidMessageRoutingKey,
        }, cancellationToken);

        return Ack.Instance;
    }
}
