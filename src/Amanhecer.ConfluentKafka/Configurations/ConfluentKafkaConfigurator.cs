using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures the Kafka gateway: the cluster connection, the publications and the
/// subscriptions used by the messaging pipeline.
/// </summary>
public class ConfluentKafkaConfigurator
{
    private string? _bootstrapServers;

    /// <summary>
    /// Sets the bootstrap servers used to connect to the cluster, in the
    /// <c>host1:port1,host2:port2</c> form.
    /// </summary>
    /// <param name="bootstrapServers">The bootstrap servers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator BootstrapServers(string bootstrapServers)
    {
        if (string.IsNullOrEmpty(bootstrapServers))
        {
            throw new ArgumentException("Bootstrap servers cannot be null or empty.", nameof(bootstrapServers));
        }

        _bootstrapServers = bootstrapServers;
        return this;
    }

    private Action<ProducerConfig>? _configureProducer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ProducerConfig"/> before a producer is
    /// created, allowing further customization.
    /// </summary>
    /// <param name="configure">The producer configuration callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator ConfigureProducer(Action<ProducerConfig> configure)
    {
        _configureProducer = configure;
        return this;
    }

    private Action<ConsumerConfig>? _configureConsumer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ConsumerConfig"/> before a consumer is
    /// created, allowing further customization.
    /// </summary>
    /// <param name="configure">The consumer configuration callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator ConfigureConsumer(Action<ConsumerConfig> configure)
    {
        _configureConsumer = configure;
        return this;
    }

    private Action<AdminClientConfig>? _configureAdmin;

    /// <summary>
    /// Sets a callback invoked with the <see cref="AdminClientConfig"/> before the admin
    /// client is created, allowing further customization.
    /// </summary>
    /// <param name="configure">The admin client configuration callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator ConfigureAdmin(Action<AdminClientConfig> configure)
    {
        _configureAdmin = configure;
        return this;
    }

    private readonly List<ConfluentKafkaPublication> _publications = [];

    /// <summary>
    /// Adds publications to the gateway, defining the messages published to Kafka topics.
    /// </summary>
    /// <param name="configure">A delegate that configures the publications.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator Publications(Action<ConfluentKafkaPublicationsConfigurator> configure)
    {
        var cfg = new ConfluentKafkaPublicationsConfigurator();
        configure.Invoke(cfg);

        _publications.AddRange(cfg.ToPublications());
        return this;
    }

    private readonly List<ConfluentKafkaSubscription> _subscriptions = [];

    /// <summary>
    /// Adds subscriptions to the gateway, defining the messages consumed from Kafka topics.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscriptions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator Subscriptions(Action<ConfluentKafkaSubscriptionsConfigurator> configure)
    {
        var cfg = new ConfluentKafkaSubscriptionsConfigurator();
        configure.Invoke(cfg);

        _subscriptions.AddRange(cfg.ToSubscriptions());
        return this;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _defaultMessageMapperType;

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by the publications
    /// and subscriptions that do not configure a message mapper themselves.
    /// </summary>
    /// <param name="messageMapper">The default message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="messageMapper"/> does
    /// not implement <see cref="IMessageMapper"/>.</exception>
    public ConfluentKafkaConfigurator DefaultMessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type messageMapper)
    {
        if (!typeof(IMessageMapper).IsAssignableFrom(messageMapper))
        {
            throw new ArgumentException(
                $"The type '{messageMapper.FullName}' does not implement IMessageMapper.",
                nameof(messageMapper));
        }

        _defaultMessageMapperType = messageMapper;
        return this;
    }

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by the publications
    /// and subscriptions that do not configure a message mapper themselves.
    /// </summary>
    /// <typeparam name="TMapper">The default message mapper implementation type.</typeparam>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaConfigurator DefaultMessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _defaultMessageMapperType = typeof(TMapper);
        return this;
    }

    internal IGateway CreateGateway()
    {
        ApplyDefaultMessageMapper();

        return new ConfluentKafkaGateway
        {
            BootstrapServers = _bootstrapServers,
            ConfigureProducer = _configureProducer,
            ConfigureConsumer = _configureConsumer,
            ConfigureAdmin = _configureAdmin,
            Publications = _publications,
            Subscriptions = _subscriptions
        };
    }

    private void ApplyDefaultMessageMapper()
    {
        if (_defaultMessageMapperType is null)
        {
            return;
        }

        foreach (var publication in _publications)
        {
            publication.MessageMapperType ??= _defaultMessageMapperType;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.MessageMapperType ??= _defaultMessageMapperType;
        }
    }
}
