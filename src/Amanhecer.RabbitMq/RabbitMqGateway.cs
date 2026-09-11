using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a RabbitMQ broker.
/// </summary>
public class RabbitMqGateway : Gateway<RabbitMqPublication, RabbitMqSubscription>
    , ILoggerFactorySupport
#if NETFRAMEWORK
    , IDisposable
#else
    , IAsyncDisposable
#endif
{
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _provisioningLock = new(1, 1);
    private bool _provisioned;
    private bool _disposed;

    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; set; }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqGateway));
        }

        if (_connection != null)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RabbitMqGateway));
            }

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
    /// Provisions the gateway <see cref="Exchange"/> and the exchange of every publication,
    /// then the provisioners declared by the publications and subscriptions. Provisioning runs
    /// at most once per gateway: later calls are no-ops, unless the first attempt failed, in
    /// which case the next call retries.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
    public override async ValueTask ProvisionerAsync()
    {
        if (_provisioned)
        {
            return;
        }

        await _provisioningLock.WaitAsync();
        try
        {
            if (_provisioned)
            {
                return;
            }

            var exchanges = new List<Exchange>();
            if (Exchange != null)
            {
                exchanges.Add(Exchange);
            }

            foreach (var publication in Publications)
            {
                if (publication.Exchange != null)
                {
                    exchanges.Add(publication.Exchange);
                }
            }

            if (exchanges.Count > 0)
            {
                var connection = await GetOrCreateAsync();

#if NETFRAMEWORK
                using var channel = connection.CreateModel();
#else
                await using var channel = await connection.CreateChannelAsync(ChannelOptions);
#endif

                var provisioned = new HashSet<string>();
                foreach (var exchange in exchanges)
                {
                    if (provisioned.Add(exchange.Name))
                    {
                        await exchange
                            .Provisioner
                            .ExecuteAsync(channel, exchange);
                    }
                }
            }

            await base.ProvisionerAsync();

            _provisioned = true;
        }
        finally
        {
            _provisioningLock.Release();
        }
    }


    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        var seen = new HashSet<string>();
        foreach (var publication in Publications)
        {
            if (!seen.Add(publication.RoutingKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate publication routing key '{publication.RoutingKey}': two publications are registered with the same routing key.");
            }
        }

        // The publish path provisions lazily: the first producer creation declares the
        // exchanges, so publish-only applications get their topology before the first publish.
        ProvisionerAsync().GetAwaiter().GetResult();

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

            var producer = new RabbitMqProducer(channel, LoggerFactory?.CreateLogger<RabbitMqProducer>());
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
            // The poller's channel is shared by all consumers of the subscription: prefetch
            // one buffer's worth of messages plus one in-flight delivery per consumer.
            var prefetchCount = (long)rabbitMqSubscription.BufferSize + rabbitMqSubscription.NumberOfConsumers;
            if (rabbitMqSubscription.BufferSize <= 0
                || rabbitMqSubscription.NumberOfConsumers <= 0
                || prefetchCount > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"The subscription for queue '{rabbitMqSubscription.QueueName}' has an invalid buffer size or number of consumers: " +
                    $"'{nameof(Subscription.BufferSize)} + {nameof(Subscription.NumberOfConsumers)}' " +
                    $"({rabbitMqSubscription.BufferSize} + {rabbitMqSubscription.NumberOfConsumers}) must be between 1 and {ushort.MaxValue}.");
            }

            var connection = GetOrCreateAsync().GetAwaiter().GetResult();
#if NETFRAMEWORK
            var channel = connection.CreateModel();
            channel.BasicQos(rabbitMqSubscription.PrefetchSize,
                (ushort)prefetchCount,
                false);
            
            poller = new RabbitMqMessagePoller(rabbitMqSubscription, channel);
            channel.BasicConsume(rabbitMqSubscription.QueueName, false, poller);
#else
            var channel = connection.CreateChannelAsync(ChannelOptions).GetAwaiter().GetResult();
            channel
                .BasicQosAsync(rabbitMqSubscription.PrefetchSize,
                    (ushort)prefetchCount,
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

        return new RabbitMqConsumer(poller, rabbitMqSubscription, LoggerFactory?.CreateLogger<RabbitMqConsumer>());
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
        _provisioningLock.Dispose();
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
        _provisioningLock.Dispose();
    }
#endif
}
