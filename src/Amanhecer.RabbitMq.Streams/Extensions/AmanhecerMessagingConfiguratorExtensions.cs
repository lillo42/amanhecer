using System;
using Amanhecer.RabbitMq.Streams.Configurations;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods for <see cref="AmanhecerMessagingConfigurator"/> that register
/// a RabbitMQ Streams gateway.
/// </summary>
public static class AmanhecerMessagingConfiguratorExtensions
{
    /// <summary>
    /// Adds a RabbitMQ Streams gateway to the messaging configuration, using the provided
    /// delegate to configure the connection, publications and subscriptions.
    /// </summary>
    /// <param name="configurator">The messaging configurator being extended.</param>
    /// <param name="configure">A delegate that configures the RabbitMQ Streams gateway.</param>
    /// <returns>The messaging configurator, for chaining.</returns>
    public static AmanhecerMessagingConfigurator UsingRabbitMqStreams(
        this AmanhecerMessagingConfigurator configurator,
        Action<RabbitMqStreamConfigurator> configure)
    {
        var cfg = new RabbitMqStreamConfigurator();
        configure.Invoke(cfg);

        return configurator.AddGateway(cfg.CreateGateway());
    }
}
