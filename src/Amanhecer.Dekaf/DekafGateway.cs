using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Dekaf;

/// <summary>
/// A gateway that publishes messages to, and consumes messages from, a Kafka cluster.
/// </summary>
public class DekafGateway : Gateway<DekafPublication, DekafSubscription>, ILoggerFactorySupport, IAsyncDisposable
{
    private readonly SemaphoreSlim _provisioningLock = new(1, 1);
    private readonly object _adminClientLock = new();

    private readonly List<DeKafProducer> _producers = [];
    private readonly List<DekafConsumer> _consumers = [];
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
    /// Gets or sets a callback invoked with the <see cref="ProducerBuilder{TKey, TValue}"/>
    /// before a producer is created, allowing further customization.
    /// </summary>
    public Action<ProducerBuilder<string, byte[]>>? ConfigureProducer { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ConsumerBuilder{TKey, TValue}"/>
    /// before a consumer is created, allowing further customization.
    /// </summary>
    public Action<ConsumerBuilder<string, byte[]>>? ConfigureConsumer { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="AdminClientBuilder"/> before the
    /// admin client is created, allowing further customization.
    /// </summary>
    public Action<AdminClientBuilder>? ConfigureAdmin { get; set; }

    internal IAdminClient GetOrCreateAdminClient()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DekafGateway));
        }

        lock (_adminClientLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DekafGateway));
            }

            if (_adminClient == null)
            {
                var builder = Kafka.CreateAdminClient();
                if (BootstrapServers != null)
                {
                    builder.WithBootstrapServers(BootstrapServers);
                }

                ConfigureAdmin?.Invoke(builder);
                _adminClient = builder.Build();
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
        // The publish path provisions lazily: the first producer creation runs the
        // provisioners, so publish-only applications get their topics before the first publish.
        ProvisionerAsync().GetAwaiter().GetResult();

        var producers = new Dictionary<string, IProducer>();

        foreach (var publication in Publications)
        {
            var builder = Kafka.CreateProducer<string, byte[]>();
            if (BootstrapServers != null)
            {
                builder.WithBootstrapServers(BootstrapServers);
            }

            ConfigureProducer?.Invoke(builder);
            publication.Configure(builder);

            var producer = new DeKafProducer(builder.Build());
            producers.Add(publication.RoutingKey, producer);
            _producers.Add(producer);
        }

        return producers;
    }

    /// <inheritdoc />
    public override IConsumer CreateConsumer(ISubscription subscription)
    {
        if (subscription is not DekafSubscription dekafSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(DekafSubscription)}.",
                nameof(subscription));
        }

        var builder = Kafka.CreateConsumer<string, byte[]>()
            .WithGroupId(dekafSubscription.GroupId)
            // Offsets are stored on settlement and committed explicitly, in batches.
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetReset(dekafSubscription.AutoOffsetReset)
            .SubscribeTo(dekafSubscription.Topic);

        if (BootstrapServers != null)
        {
            builder.WithBootstrapServers(BootstrapServers);
        }

        ConfigureConsumer?.Invoke(builder);
        dekafSubscription.Configure?.Invoke(builder);

        var consumer = new DekafConsumer(builder.Build(),
            dekafSubscription,
            LoggerFactory?.CreateLogger<DekafConsumer>());
        _consumers.Add(consumer);

        return consumer;
    }

    /// <summary>
    /// Disposes the consumers, producers and the admin client owned by this gateway.
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

        if (_adminClient != null)
        {
            await _adminClient.DisposeAsync();
        }

        _provisioningLock.Dispose();
    }
}