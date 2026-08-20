using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

public class CreateQueue : ISubscriptionProvisoner
{
    public required Exchange Exchange { get; set; }
    public required string RoutingKey { get; set; }
    public bool Durable { get; set; }
    public bool Exclusive { get; set; }
    public bool AutoDelete { get; set; }
    public IDictionary<string, object?> QueueArguments { get; set; } = new Dictionary<string, object?>();
    public IDictionary<string, object?> BindArguments { get; set; } = new Dictionary<string, object?>();

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
        await Exchange.Provisioner.ExecuteAsync(channel, Exchange);

        channel.QueueDeclare(rabbitMqSubscription.QueueName,
            Durable,
            Exclusive,
            AutoDelete,
            QueueArguments);

        channel.QueueBind(rabbitMqSubscription.QueueName, Exchange.Name, RoutingKey, BindArguments);
#else
        var channel = await connection.CreateChannelAsync();
        await Exchange.Provisioner.ExecuteAsync(channel, Exchange);

        await channel.QueueDeclareAsync(rabbitMqSubscription.QueueName,
            Durable,
            Exclusive,
            AutoDelete,
            QueueArguments);

        await channel.QueueBindAsync(rabbitMqSubscription.QueueName, Exchange.Name, RoutingKey, BindArguments);
#endif
    }
}