using System;
using Amanhecer.RabbitMq.Configurations;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods for <see cref="AmanhecerMessagingConfigurator"/> that register
/// a RabbitMQ gateway.
/// </summary>
public static class AmanhecerMessagingConfiguratorExtensions
{
    /// <summary>
    /// Adds a RabbitMQ gateway to the messaging configuration, using the provided
    /// delegate to configure the connection, publications and subscriptions.
    /// </summary>
    /// <param name="configurator">The messaging configurator being extended.</param>
    /// <param name="configure">A delegate that configures the RabbitMQ gateway.</param>
    /// <returns>The messaging configurator, for chaining.</returns>
    public static AmanhecerMessagingConfigurator UsingRabbitMQ(this AmanhecerMessagingConfigurator configurator,
        Action<RabbitMqConfigurator> configure)
    {
        var cfg = new RabbitMqConfigurator();
        configure.Invoke(cfg);

        return configurator.AddGateway(cfg.CreateGateway());
    }
}