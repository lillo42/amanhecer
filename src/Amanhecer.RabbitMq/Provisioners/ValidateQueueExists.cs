using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="ISubscriptionProvisioner"/> that validates the queue exists on the broker,
/// failing if it does not.
/// </summary>
public class ValidateQueueExists : ISubscriptionProvisioner
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
            throw new System.ArgumentException(
                $"The gateway must be a {nameof(RabbitMqGateway)}.",
                nameof(gateway));
        }

        if (subscription is not RabbitMqSubscription rabbitMqSubscription)
        {
            throw new System.ArgumentException(
                $"The subscription must be a {nameof(RabbitMqSubscription)}.",
                nameof(subscription));
        }

        var connection = await rabbitMqGateway.GetOrCreateAsync();

#if NETFRAMEWORK
        using var channel = connection.CreateModel();

        if (Exchange != null)
        {
            await Exchange.Provisioner.ExecuteAsync(channel, Exchange);
        }

        channel.QueueDeclarePassive(rabbitMqSubscription.QueueName);
#else
        await using var channel = await connection.CreateChannelAsync();

        if (Exchange != null)
        {
            await Exchange.Provisioner.ExecuteAsync(channel, Exchange);
        }

        await channel.QueueDeclarePassiveAsync(rabbitMqSubscription.QueueName);
#endif
    }
}