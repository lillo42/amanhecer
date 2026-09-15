using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions.Messaging;
using Dekaf;
using Dekaf.Admin;

namespace Amanhecer.Dekaf.Configurations;

/// <summary>
/// Configures the Kafka gateway: the cluster connection, the publications and the
/// subscriptions used by the messaging pipeline.
/// </summary>
public class DekafConfigurator
{
    private string? _bootstrapServers;

    /// <summary>
    /// Sets the bootstrap servers used to connect to the cluster, in the
    /// <c>host1:port1,host2:port2</c> form.
    /// </summary>
    /// <param name="bootstrapServers">The bootstrap servers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator BootstrapServers(string bootstrapServers)
    {
        if (string.IsNullOrEmpty(bootstrapServers))
        {
            throw new ArgumentException("Bootstrap servers cannot be null or empty.", nameof(bootstrapServers));
        }

        _bootstrapServers = bootstrapServers;
        return this;
    }

    private Action<ProducerBuilder<string, byte[]>>? _configureProducer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ProducerBuilder{TKey, TValue}"/> before
    /// a producer is created, allowing further customization.
    /// </summary>
    /// <param name="configure">The producer builder callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator ConfigureProducer(Action<ProducerBuilder<string, byte[]>> configure)
    {
        _configureProducer = configure;
        return this;
    }

    private Action<ConsumerBuilder<string, byte[]>>? _configureConsumer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ConsumerBuilder{TKey, TValue}"/> before
    /// a consumer is created, allowing further customization.
    /// </summary>
    /// <param name="configure">The consumer builder callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator ConfigureConsumer(Action<ConsumerBuilder<string, byte[]>> configure)
    {
        _configureConsumer = configure;
        return this;
    }

    private Action<AdminClientBuilder>? _configureAdmin;

    /// <summary>
    /// Sets a callback invoked with the <see cref="AdminClientBuilder"/> before the admin
    /// client is created, allowing further customization.
    /// </summary>
    /// <param name="configure">The admin client builder callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator ConfigureAdmin(Action<AdminClientBuilder> configure)
    {
        _configureAdmin = configure;
        return this;
    }

    private readonly List<DekafPublication> _publications = [];

    /// <summary>
    /// Adds publications to the gateway, defining the messages published to Kafka topics.
    /// </summary>
    /// <param name="configure">A delegate that configures the publications.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator Publications(Action<DekafPublicationsConfigurator> configure)
    {
        var cfg = new DekafPublicationsConfigurator();
        configure.Invoke(cfg);

        _publications.AddRange(cfg.ToPublications());
        return this;
    }

    private readonly List<DekafSubscription> _subscriptions = [];

    /// <summary>
    /// Adds subscriptions to the gateway, defining the messages consumed from Kafka topics.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscriptions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafConfigurator Subscriptions(Action<DekafSubscriptionsConfigurator> configure)
    {
        var cfg = new DekafSubscriptionsConfigurator();
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
    public DekafConfigurator DefaultMessageMapper(
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
    public DekafConfigurator DefaultMessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _defaultMessageMapperType = typeof(TMapper);
        return this;
    }

    internal IGateway CreateGateway()
    {
        ApplyDefaultMessageMapper();

        return new DekafGateway
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
