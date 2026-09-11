using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Extensions.Hosting.Extensions;

/// <summary>
/// Extension methods to register the Amanhecer message consumers with the host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ConsumerHostedService"/>, which starts the message consumers
    /// for all registered gateways together with the host lifetime.
    /// </summary>
    /// <param name="services">The service collection to register the hosted service with.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAmanhecerHost(this IServiceCollection services)
    {
        return services.AddHostedService<ConsumerHostedService>();
    }
}
