using System;
using Amanhecer.ConfluentKafka.Configurations;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods for <see cref="AmanhecerMessagingConfigurator"/> that register
/// a Kafka gateway backed by Confluent.Kafka.
/// </summary>
public static class AmanhecerMessagingConfiguratorExtensions
{
    /// <summary>
    /// Adds a Kafka gateway (Confluent.Kafka) to the messaging configuration, using the
    /// provided delegate to configure the connection, publications and subscriptions.
    /// </summary>
    /// <param name="configurator">The messaging configurator being extended.</param>
    /// <param name="configure">A delegate that configures the Kafka gateway.</param>
    /// <returns>The messaging configurator, for chaining.</returns>
    public static AmanhecerMessagingConfigurator UsingConfluentKafka(this AmanhecerMessagingConfigurator configurator,
        Action<ConfluentKafkaConfigurator> configure)
    {
        var cfg = new ConfluentKafkaConfigurator();
        configure.Invoke(cfg);

        return configurator.AddGateway(cfg.CreateGateway());
    }
}
