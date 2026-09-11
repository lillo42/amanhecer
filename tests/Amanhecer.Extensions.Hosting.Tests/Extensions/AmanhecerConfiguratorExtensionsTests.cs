using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Configurator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Amanhecer.Extensions.Hosting.Tests.Extensions;

public class AmanhecerConfiguratorExtensionsTests
{
    [Test]
    public async Task When_AddHostedService_Should_ReturnTheSameConfigurator()
    {
        var configurator = new AmanhecerConfigurator(new ServiceCollection());

        var result = configurator.AddHostedService();

        await Assert.That(ReferenceEquals(result, configurator)).IsTrue();
    }

    [Test]
    public async Task When_AddHostedService_Should_RegisterConsumerHostedService()
    {
        var configurator = new AmanhecerConfigurator(new ServiceCollection());

        configurator.AddHostedService();

        var descriptor = configurator.Services.Single(d => d.ServiceType == typeof(IHostedService));
        await Assert.That(descriptor.ImplementationType).IsEqualTo(typeof(ConsumerHostedService));
        await Assert.That(descriptor.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task When_AddHostedService_WhenCalledTwice_Should_RegisterTheHostedServiceOnlyOnce()
    {
        var configurator = new AmanhecerConfigurator(new ServiceCollection());

        configurator.AddHostedService();
        configurator.AddHostedService();

        await Assert.That(configurator.Services.Count(d => d.ServiceType == typeof(IHostedService))).IsEqualTo(1);
    }
}
