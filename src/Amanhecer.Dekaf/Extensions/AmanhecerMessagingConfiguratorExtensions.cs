using System;
using Amanhecer.Dekaf.Configurations;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods for <see cref="AmanhecerMessagingConfigurator"/> that register
/// a Kafka gateway backed by Dekaf.
/// </summary>
public static class AmanhecerMessagingConfiguratorExtensions
{
    /// <summary>
    /// Adds a Kafka gateway (Dekaf) to the messaging configuration, using the provided
    /// delegate to configure the connection, publications and subscriptions.
    /// </summary>
    /// <param name="configurator">The messaging configurator being extended.</param>
    /// <param name="configure">A delegate that configures the Kafka gateway.</param>
    /// <returns>The messaging configurator, for chaining.</returns>
    public static AmanhecerMessagingConfigurator UsingDekaf(this AmanhecerMessagingConfigurator configurator,
        Action<DekafConfigurator> configure)
    {
        var cfg = new DekafConfigurator();
        configure.Invoke(cfg);

        return configurator.AddGateway(cfg.CreateGateway());
    }
}
