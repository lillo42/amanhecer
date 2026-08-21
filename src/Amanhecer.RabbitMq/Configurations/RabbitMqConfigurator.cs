using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures the RabbitMQ gateway: the broker connection, the publications and
/// the subscriptions used by the messaging pipeline.
/// </summary>
public class RabbitMqConfigurator
{
    private ConnectionFactory? _connectionFactory;

    /// <summary>
    /// Configures the connection to the RabbitMQ broker.
    /// </summary>
    /// <param name="configure">A delegate that configures the connection settings.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConfigurator Connection(Action<RabbitMqConnectionConfigurator> configure)
    {
        var cfg = new RabbitMqConnectionConfigurator();
        configure.Invoke(cfg);

        _connectionFactory = new ConnectionFactory();
        cfg.ApplyTo(_connectionFactory);
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

    internal IGateway CreateGateway()
    {
        return new RabbitMqGateway
        {
            Publications = _publications,
            Subscriptions = _subscriptions
        };
    }
}