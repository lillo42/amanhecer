using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="ISubscriptionProvisoner"/> that validates the queue exists on the broker,
/// failing if it does not.
/// </summary>
public class ValidateQueueExists : ISubscriptionProvisoner
{
    /// <summary>
    /// Gets or sets the <see cref="RabbitMq.Exchange"/> to validate before the queue, if any.
    /// </summary>
    public Exchange? Exchange { get; set; }

    /// <inheritdoc />
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