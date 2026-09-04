using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IConsumer
{
    ISubscription Subscription { get; }

    ValueTask AckAsync(Message message);

    ValueTask NackAsync(Message message);

    ValueTask DeferAsync(Message message, TimeSpan delay);

    ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default);
}