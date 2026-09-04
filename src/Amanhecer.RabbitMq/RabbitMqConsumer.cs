using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

public class RabbitMqConsumer(RabbitMqMessagePoller poller, RabbitMqSubscription subscription) : IConsumer
{
    public ISubscription Subscription { get; } = subscription;

#if NETFRAMEWORK
    public ValueTask AckAsync(Message message)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return new ValueTask();
        }

        poller.Model.BasicAck(deliveryTag, false);
        return new ValueTask();
    }

    public ValueTask NackAsync(Message message)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return new ValueTask();
        }

        poller.Model.BasicNack(deliveryTag, false, false);
        return new ValueTask();
    }

    public ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return new ValueTask();
        }

        poller.Model.BasicNack(deliveryTag, false, true);
        return new ValueTask();
    }
#else
    public async ValueTask AckAsync(Message message)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return;
        }

        await poller.Channel.BasicAckAsync(deliveryTag, false);
    }

    public async ValueTask NackAsync(Message message)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return;
        }

        await poller.Channel.BasicNackAsync(deliveryTag, false, false);
    }

    public async ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (!message.Metadata.TryGetValue(MetadataName.DeliveryTag, out var obj)
            || obj is not ulong deliveryTag)
        {
            return;
        }

        await poller.Channel.BasicNackAsync(deliveryTag, false, true);
    }
#endif

    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var message = await poller.Messages.ReadAsync(cancellationToken);
        return [message];
    }
}