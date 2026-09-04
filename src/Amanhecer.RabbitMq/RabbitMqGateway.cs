using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a RabbitMQ broker.
/// </summary>
public class RabbitMqGateway : Gateway<RabbitMqPublication, RabbitMqSubscription>
{
    private IConnection? _connection;

    /// <summary>
    /// Gets or sets the AMQP URI used to connect to the broker.
    /// </summary>
    public Uri? AmqpUri { get; set; }

    /// <summary>
    /// Gets or sets the exchange provisioned before the gateway is used.
    /// </summary>
    public Exchange? Exchange { get; set; }

    /// <summary>
    /// Gets or sets the user id associated with this gateway.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the application id associated with this gateway.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Gets or sets the cluster id associated with this gateway.
    /// </summary>
    public string? ClusterId { get; set; }

#if !NETFRAMEWORK
    /// <summary>
    /// Gets or sets the options used to create the channels of this gateway.
    /// </summary>
    public CreateChannelOptions? ChannelOptions { get; set; }
#endif

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ConnectionFactory"/> before the
    /// connection is created, allowing further customization.
    /// </summary>
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
        _connection = factory.CreateConnection();
        return new ValueTask<IConnection>(_connection);
#else
        return new ValueTask<IConnection>(Create());

        async Task<IConnection> Create()
        {
            _connection = await factory.CreateConnectionAsync();
            return _connection;
        }
#endif
    }


    /// <summary>
    /// Provisions the gateway <see cref="Exchange"/>, if one is set, then the provisioners
    /// declared by the publications and subscriptions.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
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

        foreach (var subscription in Subscriptions)
        {
            if (subscription.Provisioner != null)
            {
                await subscription.Provisioner
                    .ExecuteAsync(this, subscription);
            }
        }

        await base.ProvisionerAsync();
    }


    /// <inheritdoc />
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

    private readonly Dictionary<RabbitMqSubscription, RabbitMqMessagePoller> _pollers = [];

    /// <inheritdoc />
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not RabbitMqSubscription rabbitMqSubscription)
        {
            throw new NotImplementedException();
        }

        if (!_pollers.TryGetValue(rabbitMqSubscription, out var poller))
        {
            var connection = GetOrCreateAsync().GetAwaiter().GetResult();
#if NETFRAMEWORK
            var channel = connection.CreateModel();
#else
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
#endif

            poller = new RabbitMqMessagePoller(rabbitMqSubscription, channel);
            _pollers.Add(rabbitMqSubscription, poller);
        }

        return new RabbitMqConsumer(poller, rabbitMqSubscription);
    }
}