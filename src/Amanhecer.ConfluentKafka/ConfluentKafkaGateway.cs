using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a Kafka cluster.
/// </summary>
public class ConfluentKafkaGateway : Gateway<ConfluentKafkaPublication, ConfluentKafkaSubscription>
    , ILoggerFactorySupport
    , IDisposable
{
    private readonly SemaphoreSlim _provisioningLock = new(1, 1);
    private readonly List<ConfluentKafkaProducer> _producers = [];
    private readonly List<ConfluentKafkaConsumer> _consumers = [];
    private readonly object _adminClientLock = new();
    private IAdminClient? _adminClient;
    private bool _provisioned;
    private bool _disposed;

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

        lock (_adminClientLock)
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
    }

    /// <summary>
    /// Executes the provisioners declared by the publications and subscriptions. Provisioning
    /// runs at most once per gateway: later calls are no-ops, unless the first attempt failed,
    /// in which case the next call retries.
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

        // The publish path provisions lazily: the first producer creation runs the
        // provisioners, so publish-only applications get their topics before the first publish.
        ProvisionerAsync().GetAwaiter().GetResult();

        var producers = new Dictionary<string, IProducer>();

        foreach (var publication in Publications)
        {
            var config = new ProducerConfig { BootstrapServers = BootstrapServers };
            ConfigureProducer?.Invoke(config);

            var producer = new ConfluentKafkaProducer(
                new ProducerBuilder<string?, byte[]>(config).Build());
            producers.Add(publication.RoutingKey, producer);
            _producers.Add(producer);
        }

        return producers;
    }

    /// <inheritdoc />
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

        var consumer = new ConfluentKafkaConsumer(config,
            kafkaSubscription,
            LoggerFactory?.CreateLogger<ConfluentKafkaConsumer>());
        _consumers.Add(consumer);

        return consumer;
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

        foreach (var consumer in _consumers)
        {
            consumer.Dispose();
        }

        _consumers.Clear();

        foreach (var producer in _producers)
        {
            producer.Dispose();
        }

        _producers.Clear();
        _adminClient?.Dispose();
        _provisioningLock.Dispose();
    }
}
