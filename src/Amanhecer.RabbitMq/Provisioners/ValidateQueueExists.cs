using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

public class ValidateQueueExists : ISubscriptionProvisoner
{
    public Exchange? Exchange { get; set; }

    public async Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        if (gateway is not RabbitMqGateway rabbitMqGateway)
        {
            throw new System.NotImplementedException();
        }

        if (subscription is not RabbitMqSubscription rabbitMqSubscription)
        {
            throw new System.NotImplementedException();
        }

        var connection = await rabbitMqGateway.GetOrCreateAsync();

#if NETFRAMEWORK
        var channel = connection.CreateModel();

        if (Exchange != null)
        {
            await Exchange.Provisioner.ExecuteAsync(channel, Exchange);
        }

        channel.QueueDeclarePassive(rabbitMqSubscription.QueueName);
#else
        var channel = await connection.CreateChannelAsync();

        if (Exchange != null)
        {
            await Exchange.Provisioner.ExecuteAsync(channel, Exchange);
        }

        await channel.QueueDeclarePassiveAsync(rabbitMqSubscription.QueueName);
#endif
    }
}