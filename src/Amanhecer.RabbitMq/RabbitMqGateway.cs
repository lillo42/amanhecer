using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a RabbitMQ broker.
/// </summary>
public class RabbitMqGateway : Gateway<RabbitMqPublication, RabbitMqSubscription>
#if NETFRAMEWORK
    , IDisposable
#else
    , IAsyncDisposable
#endif
{
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Gets or sets the AMQP URI used to connect to the broker.
    /// </summary>
    public Uri? AmqpUri { get; set; }

    /// <summary>
    /// Gets or sets the exchange provisioned before the gateway is used.
    /// </summary>
    public Exchange? Exchange { get; set; }

#if !NETFRAMEWORK
    /// <summary>
    /// Gets or sets the options used to create the channels of this gateway. Publications can
    /// override them per channel via <see cref="RabbitMqPublication.ChannelOptions"/>.
    /// </summary>
    public CreateChannelOptions? ChannelOptions { get; set; }
#endif

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ConnectionFactory"/> before the
    /// connection is created, allowing further customization.
    /// </summary>
    public Action<ConnectionFactory>? Configure { get; set; }

    internal async ValueTask<IConnection> GetOrCreateAsync()
    {
        if (_connection != null)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null)
            {
                return _connection;
            }

            var factory = new ConnectionFactory();
            if (AmqpUri != null)
            {
                factory.Uri = AmqpUri;
            }

            Configure?.Invoke(factory);

#if NETFRAMEWORK
            _connection = factory.CreateConnection();
#else
            _connection = await factory.CreateConnectionAsync();
#endif
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Provisions the gateway <see cref="Exchange"/>, if one is set, then the provisioners
    /// declared by the publications and subscriptions.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
    public override async ValueTask ProvisionerAsync()
    {
        if (Exchange != null)
        {
            var connection = await GetOrCreateAsync();

#if NETFRAMEWORK
            using var channel = connection.CreateModel();
#else
            await using var channel = await connection.CreateChannelAsync(ChannelOptions);
#endif

            await Exchange
                .Provisioner
                .ExecuteAsync(channel, Exchange);
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
            var channel = connection
                .CreateChannelAsync(publication.ChannelOptions ?? ChannelOptions)
                .GetAwaiter()
                .GetResult();
#endif

            var producer = new RabbitMqProducer(channel);
            producers.Add(publication.RoutingKey, producer);
            _producers.Add(producer);
        }

        return producers;
    }

    private readonly Dictionary<RabbitMqSubscription, RabbitMqMessagePoller> _pollers = [];
    private readonly List<RabbitMqProducer> _producers = [];

    /// <inheritdoc />
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not RabbitMqSubscription rabbitMqSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(RabbitMqSubscription)}.",
                nameof(subscription));
        }

        if (!_pollers.TryGetValue(rabbitMqSubscription, out var poller))
        {
            var connection = GetOrCreateAsync().GetAwaiter().GetResult();
#if NETFRAMEWORK
            var channel = connection.CreateModel();
            channel.BasicQos(rabbitMqSubscription.PrefetchSize, 
                (ushort)(rabbitMqSubscription.BufferSize * rabbitMqSubscription.NumberOfConsumers), 
                false);
            
            poller = new RabbitMqMessagePoller(rabbitMqSubscription, channel);
            channel.BasicConsume(rabbitMqSubscription.QueueName, false, poller);
#else
            var channel = connection.CreateChannelAsync(ChannelOptions).GetAwaiter().GetResult();
            channel
                .BasicQosAsync(rabbitMqSubscription.PrefetchSize,
                    (ushort)(rabbitMqSubscription.BufferSize * rabbitMqSubscription.NumberOfConsumers), 
                    false)
                .GetAwaiter()
                .GetResult();
            
            poller = new RabbitMqMessagePoller(rabbitMqSubscription, channel);
            channel
                .BasicConsumeAsync(rabbitMqSubscription.QueueName, false, poller)
                .GetAwaiter()
                .GetResult();
#endif

            _pollers.Add(rabbitMqSubscription, poller);
        }

        return new RabbitMqConsumer(poller, rabbitMqSubscription);
    }

#if NETFRAMEWORK
    /// <summary>
    /// Disposes the poller channels and the connection owned by this gateway.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var producer in _producers)
        {
            producer.Dispose();
        }

        _producers.Clear();

        foreach (var poller in _pollers.Values)
        {
            poller.Complete();
            poller.Model.Dispose();
        }

        _pollers.Clear();
        _connection?.Dispose();
        _connectionLock.Dispose();
    }
#else
    /// <summary>
    /// Disposes the poller channels and the connection owned by this gateway.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the gateway has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var producer in _producers)
        {
            await producer.DisposeAsync();
        }

        _producers.Clear();

        foreach (var poller in _pollers.Values)
        {
            poller.Complete();
            await poller.Channel.DisposeAsync();
        }

        _pollers.Clear();

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
#endif
}
