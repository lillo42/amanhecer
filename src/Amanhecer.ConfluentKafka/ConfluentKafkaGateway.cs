using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a Kafka cluster.
/// </summary>
public class ConfluentKafkaGateway : Gateway<ConfluentKafkaPublication, ConfluentKafkaSubscription>,
    ILoggerFactorySupport, IDisposable
{
    private bool _disposed;
    private IAdminClient? _adminClient;

    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets the bootstrap servers used to connect to the cluster, in the
    /// <c>host1:port1,host2:port2</c> form.
    /// </summary>
    public string? BootstrapServers { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ProducerConfig"/> before a
    /// producer is created, allowing further customization.
    /// </summary>
    public Action<ProducerConfig>? ConfigureProducer { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ConsumerConfig"/> before a
    /// consumer is created, allowing further customization.
    /// </summary>
    public Action<ConsumerConfig>? ConfigureConsumer { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="AdminClientConfig"/> before the
    /// admin client is created, allowing further customization.
    /// </summary>
    public Action<AdminClientConfig>? ConfigureAdmin { get; set; }

    internal IAdminClient GetOrCreateAdminClient()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfluentKafkaGateway));
        }

        if (_adminClient == null)
        {
            var config = new AdminClientConfig { BootstrapServers = BootstrapServers };
            ConfigureAdmin?.Invoke(config);
            _adminClient = new AdminClientBuilder(config).Build();
        }

        return _adminClient;
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IProducer> CreateProducers()
    {
        var producers = new Dictionary<string, IProducer>();

        foreach (var publication in Publications)
        {
            var config = new ProducerConfig { BootstrapServers = BootstrapServers };
            ConfigureProducer?.Invoke(config);
            publication.ConfigureProducer.Invoke(config);

            var producer = new ConfluentKafkaProducer(new ProducerBuilder<string?, byte[]>(config).Build());
            producers.Add(publication.RoutingKey, producer);
        }

        return producers;
    }

    /// <summary>
    /// Creates, or hands back, one of the consumers of the subscription. Each consumer is a
    /// member of the subscription's consumer group, so the gateway keeps
    /// <see cref="Subscription.NumberOfConsumers"/> of them per subscription and reuses them:
    /// the hosted service creates its consumers again on every start, and building new clients
    /// would leave the previous ones joined to the group without anything polling them.
    /// </summary>
    /// <param name="subscription">The subscription to consume for.</param>
    /// <returns>The consumer to poll.</returns>
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not ConfluentKafkaSubscription kafkaSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(ConfluentKafkaSubscription)}.",
                nameof(subscription));
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = kafkaSubscription.GroupId,
            AutoOffsetReset = kafkaSubscription.AutoOffsetReset,
            // Offsets are stored on settlement and committed explicitly, in batches.
            EnableAutoOffsetStore = false,
            EnableAutoCommit = false,
            // Topics are provisioned explicitly through the gateway's provisioners.
            AllowAutoCreateTopics = false
        };

        ConfigureConsumer?.Invoke(config);
        kafkaSubscription.Configure?.Invoke(config);

        return new ConfluentKafkaConsumer(config,
            kafkaSubscription,
            LoggerFactory?.CreateLogger<ConfluentKafkaConsumer>());
    }

    /// <summary>
    /// Disposes the consumers, producers and the admin client owned by this gateway.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _adminClient?.Dispose();
    }
}