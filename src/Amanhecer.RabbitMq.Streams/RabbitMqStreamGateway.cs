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
    ILoggerFactorySupport
{
    private StreamSystem? _system;

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

    private ValueTask<StreamSystem> GetOrCreateStreamSystem()
    {
        if (_system != null)
        {
            return new ValueTask<StreamSystem>(_system);
        }

        return new ValueTask<StreamSystem>(CreateStreamSystem());

        async Task<StreamSystem> CreateStreamSystem()
        {
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
            producers.Add(publication.RoutingKey, new RabbitMqStreamProducer(producer));
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
            InitialCredits = (ushort)Math.Min(ushort.MaxValue, rmqStreamSubscription.BufferSize)
        };

        rmqStreamSubscription.Configuration?.Invoke(config);

        var consumer = new RabbitMqStreamConsumer(config, rmqStreamSubscription, 
            LoggerFactory?.CreateLogger<RabbitMqStreamConsumer>() ?? new NullLogger<RabbitMqStreamConsumer>());
        consumer.InitAsync().GetAwaiter().GetResult();
        return consumer;
    }
}