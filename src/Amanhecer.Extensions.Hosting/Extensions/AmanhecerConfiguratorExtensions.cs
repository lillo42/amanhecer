using Amanhecer.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Configurator;

/// <summary>
/// Extension methods to register the Amanhecer message consumers with the host.
/// </summary>
public static class AmanhecerConfiguratorExtensions
{
    /// <summary>
    /// Registers the <see cref="ConsumerHostedService"/>, which starts the message consumers
    /// for all registered gateways together with the host lifetime.
    /// </summary>
    /// <param name="configurator">The configurator whose service collection the hosted service
    /// is registered with.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public static AmanhecerConfigurator AddHostedService(this AmanhecerConfigurator configurator)
    {
        configurator.Services.AddHostedService<ConsumerHostedService>();
        return configurator;
    }
}
