using System;
using Amanhecer.InMemory.Configurations;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods for <see cref="AmanhecerMessagingConfigurator"/> that register an
/// in-memory gateway.
/// </summary>
public static class AmanhecerMessagingConfiguratorExtensions
{
    /// <summary>
    /// Adds an in-memory gateway to the messaging configuration, using the provided delegate to
    /// configure publications and subscriptions.
    /// </summary>
    /// <param name="configurator">The messaging configurator being extended.</param>
    /// <param name="configure">A delegate that configures the in-memory gateway.</param>
    /// <returns>The messaging configurator, for chaining.</returns>
    public static AmanhecerMessagingConfigurator UsingInMemory(this AmanhecerMessagingConfigurator configurator,
        Action<InMemoryConfigurator> configure)
    {
        var cfg = new InMemoryConfigurator();
        configure.Invoke(cfg);

        return configurator.AddGateway(cfg.CreateGateway());
    }
}
