using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

public class RabbitMqGateway : Gateway<RabbitMqPublication, RabbitMqSubscription>
{
    private IConnection? _connection;
    public Uri? AmqpUri { get; set; }
    public Exchange? Exchange { get; set; }

    public string? UserId { get; set; }
    public string? AppId { get; set; }
    public string? ClusterId { get; set; }

#if !NETFRAMEWORK
    public CreateChannelOptions? ChannelOptions { get; set; }
#endif

    public Action<ConnectionFactory>? Configure { get; set; }

    internal ValueTask<IConnection> GetOrCreateAsync()
    {
        if (_connection != null)
        {
            return new ValueTask<IConnection>(_connection);
        }

        var factory = new ConnectionFactory();
        if (AmqpUri != null)
        {
            factory.Uri = AmqpUri;
        }

        Configure?.Invoke(factory);

#if NETFRAMEWORK
        return new ValueTask<IConnection>(factory.CreateConnection());
#else
        return new ValueTask<IConnection>(factory.CreateConnectionAsync());
#endif
    }


    public override async ValueTask ProvisionerAsync()
    {
        var connection = await GetOrCreateAsync();

        if (Exchange != null)
        {
#if NETFRAMEWORK
            var channel = connection.CreateModel();
#else
            var channel = await connection.CreateChannelAsync();
#endif

            await Exchange
                .Provisioner
                .ExecuteAsync(channel, Exchange);
        }

        await base.ProvisionerAsync();
    }


    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        var producers = new Dictionary<string, IProducer>();
        var connection = GetOrCreateAsync().GetAwaiter().GetResult();
        
        foreach (var publication in Publications)
        {
#if NETFRAMEWORK
            var channel = connection.CreateModel();
#else
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
#endif

            producers.Add(publication.RoutingKey, new RabbitMqProducer(channel));
        }

        return producers;
    }

    public override IEnumerable<IConsumer> CreateSubscriptions()
    {
        var producers = new List<IConsumer>();
        var connection = GetOrCreateAsync().GetAwaiter().GetResult();
        
        foreach (var subscription in Subscriptions)
        {
#if NETFRAMEWORK
            var channel = connection.CreateModel();
#else
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
#endif

            var consumer = new RabbitMqConsumer(subscription, channel);
            producers.Add(consumer);
        }

        return producers;
    }
}