using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures the RabbitMQ gateway: the broker connection, the publications and
/// the subscriptions used by the messaging pipeline.
/// </summary>
public class RabbitMqConfigurator
{
    private RabbitMqConnectionConfigurator? _connection;

    /// <summary>
    /// Configures the connection to the RabbitMQ broker.
    /// </summary>
    /// <param name="configure">A delegate that configures the connection settings.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConfigurator Connection(Action<RabbitMqConnectionConfigurator> configure)
    {
        var cfg = new RabbitMqConnectionConfigurator();
        configure.Invoke(cfg);

        _connection = cfg;
        return this;
    }


    private readonly List<RabbitMqPublication> _publications = [];

    /// <summary>
    /// Adds publications to the gateway, defining the messages published to RabbitMQ exchanges.
    /// </summary>
    /// <param name="configure">A delegate that configures the publications.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConfigurator Publications(Action<RabbitMqPublicationsConfigurator> configure)
    {
        var cfg = new RabbitMqPublicationsConfigurator();
        configure.Invoke(cfg);

        _publications.AddRange(cfg.ToPublications());
        return this;
    }

    private readonly List<RabbitMqSubscription> _subscriptions = [];

    /// <summary>
    /// Adds subscriptions to the gateway, defining the messages consumed from RabbitMQ queues.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscriptions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConfigurator Subscriptions(Action<RabbitMqSubscriptionsConfigurator> configure)
    {
        var cfg = new RabbitMqSubscriptionsConfigurator();
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
    public RabbitMqConfigurator DefaultMessageMapper(
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
    public RabbitMqConfigurator DefaultMessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _defaultMessageMapperType = typeof(TMapper);
        return this;
    }

    internal IGateway CreateGateway()
    {
        ApplyDefaultMessageMapper();

        var gateway = new RabbitMqGateway
        {
            Publications = _publications,
            Subscriptions = _subscriptions
        };

        if (_connection is not null)
        {
            gateway.Configure = _connection.ApplyTo;
        }

        return gateway;
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