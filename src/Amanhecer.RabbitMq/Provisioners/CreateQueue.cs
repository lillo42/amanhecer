using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="ISubscriptionProvisioner"/> that declares a queue and binds it to an exchange,
/// creating them if they do not exist yet.
/// </summary>
public class CreateQueue : ISubscriptionProvisioner
{
    /// <summary>
    /// Gets or sets the <see cref="RabbitMq.Exchange"/> the queue is bound to.
    /// </summary>
    public required Exchange Exchange { get; set; }

    /// <summary>
    /// Gets or sets the routing key used for the binding between the queue and the exchange.
    /// </summary>
    public required string RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue survives a broker restart.
    /// </summary>
    public bool Durable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue can only be used by the connection that declared it.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is deleted when it is no longer in use.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets additional arguments passed to the queue declaration.
    /// </summary>
    public IDictionary<string, object?> QueueArguments { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or sets additional arguments passed to the queue binding.
    /// </summary>
    public IDictionary<string, object?> BindArguments { get; set; } = new Dictionary<string, object?>();

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
        await Exchange.Provisioner.ExecuteAsync(channel, Exchange);

        channel.QueueDeclare(rabbitMqSubscription.QueueName,
            Durable,
            Exclusive,
            AutoDelete,
            QueueArguments);

        channel.QueueBind(rabbitMqSubscription.QueueName, Exchange.Name, RoutingKey, BindArguments);
#else
        await using var channel = await connection.CreateChannelAsync();
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