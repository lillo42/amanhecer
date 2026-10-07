using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;
using IConsumer = Amanhecer.Abstractions.Messaging.IConsumer;
using IProducer = Amanhecer.Abstractions.Messaging.IProducer;

namespace Amanhecer.RabbitMq.Streams;

/// <summary>Gateway that connects to a RabbitMQ Streams broker and creates producers and consumers for declared publications and subscriptions.</summary>
public class RabbitMqStreamGateway : Gateway<RabbitMqStreamPublication, RabbitMqStreamSubscription>,
    ILoggerFactorySupport, IAsyncDisposable
{
    private readonly List<RabbitMqStreamProducer> _producers = [];
    private readonly List<RabbitMqStreamConsumer> _consumers = [];
    private StreamSystem? _system;
    private bool _disposed;

    /// <summary>Gets or sets the broker user name. Defaults to <c>guest</c>.</summary>
    public string UserName { get; set; } = "guest";

    /// <summary>Gets or sets the broker password. Defaults to <c>guest</c>.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>Gets or sets the virtual host. Defaults to <c>/</c>.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Gets or sets the broker endpoints. Defaults to <c>localhost:5552</c>.</summary>
    public IList<EndPoint> EndPoints { get; set; } = [new IPEndPoint(IPAddress.Loopback, 5552)];

    /// <summary>Gets or sets the user id stamped on published messages.</summary>
    public string? UserId { get; set; }

    /// <inheritdoc/>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Gets or sets an optional callback to further configure the <see cref="StreamSystemConfig"/> before connecting.</summary>
    public Action<StreamSystemConfig>? Configuration { get; set; }

    internal async ValueTask<StreamSystem> GetOrCreateStreamSystem()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_system != null)
        {
            return _system;
        }

        var config = new StreamSystemConfig
        {
            UserName = UserName,
            Password = Password,
            VirtualHost = VirtualHost,
            Endpoints = EndPoints
        };

        Configuration?.Invoke(config);

        _system = await StreamSystem.Create(config, LoggerFactory?.CreateLogger<StreamSystem>());
        return _system;
    }

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        var getOrCreateStreamSystem = GetOrCreateStreamSystem();
        var streamSystem = getOrCreateStreamSystem.IsCompletedSuccessfully
            ? getOrCreateStreamSystem.Result
            : getOrCreateStreamSystem.AsTask().GetAwaiter().GetResult();

        var producers = new Dictionary<string, IProducer>();
        foreach (var publication in Publications)
        {
            publication.UserId ??= UserId;

            var config = new ProducerConfig(streamSystem, publication.Stream);
            publication.Configure?.Invoke(config);

            var producer = Producer.Create(config).GetAwaiter().GetResult();
            var rabbitMqProducer = new RabbitMqStreamProducer(producer);
            _producers.Add(rabbitMqProducer);
            producers.Add(publication.RoutingKey, rabbitMqProducer);
        }

        return producers;
    }

    /// <inheritdoc/>
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not RabbitMqStreamSubscription rmqStreamSubscription)
        {
            throw new ArgumentException("Subscription must be of type RabbitMqStreamSubscription");
        }

        var getOrCreateStreamSystem = GetOrCreateStreamSystem();
        var streamSystem = getOrCreateStreamSystem.IsCompletedSuccessfully
            ? getOrCreateStreamSystem.Result
            : getOrCreateStreamSystem.AsTask().GetAwaiter().GetResult();

        var config = new ConsumerConfig(streamSystem, rmqStreamSubscription.Stream)
        {
            InitialCredits = (ushort)Math.Min(ushort.MaxValue, rmqStreamSubscription.BufferSize),
            // Offset tracking (ack) requires a reference name; defaults to the subscription name.
            Reference = rmqStreamSubscription.Name
        };

        rmqStreamSubscription.Configuration?.Invoke(config);

        var consumer = new RabbitMqStreamConsumer(config, rmqStreamSubscription,
            LoggerFactory?.CreateLogger<RabbitMqStreamConsumer>() ?? new NullLogger<RabbitMqStreamConsumer>());
        consumer.InitAsync().GetAwaiter().GetResult();
        _consumers.Add(consumer);
        return consumer;
    }

    /// <summary>
    /// Disposes the producers and consumers created by this gateway and closes the underlying
    /// <see cref="StreamSystem"/>.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the gateway has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var consumer in _consumers)
        {
            await consumer.DisposeAsync();
        }

        _consumers.Clear();

        foreach (var producer in _producers)
        {
            await producer.DisposeAsync();
        }

        _producers.Clear();

        if (_system != null)
        {
            await _system.Close();
            _system = null;
        }
    }
}
