using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// A <see cref="IResolvingConsumerAction"/> that reposts the message to the subscription's
/// invalid-message routing key and then acknowledges it.
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
        if (subscription.InvalidMessageRoutingKey != null)
        {
            await dispatcher.PostAsync(message, new AmanhecerContext
            {
                RoutingKey = subscription.InvalidMessageRoutingKey,
            }, cancellationToken);
        }

        return Ack.Instance;
    }
}