using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Extensions.Hosting.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Amanhecer.Extensions.Hosting.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task When_AddAmanhecerHost_Should_ReturnTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAmanhecerHost();

        await Assert.That(ReferenceEquals(result, services)).IsTrue();
    }

    [Test]
    public async Task When_AddAmanhecerHost_Should_RegisterConsumerHostedServiceAsSingletonHostedService()
    {
        var services = new ServiceCollection();

        services.AddAmanhecerHost();

        var descriptor = services.Single(d => d.ServiceType == typeof(IHostedService));
        await Assert.That(descriptor.ImplementationType).IsEqualTo(typeof(ConsumerHostedService));
        await Assert.That(descriptor.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task When_AddAmanhecerHost_WhenCalledTwice_Should_RegisterTheHostedServiceOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddAmanhecerHost();
        services.AddAmanhecerHost();

        await Assert.That(services.Count(d => d.ServiceType == typeof(IHostedService))).IsEqualTo(1);
    }

    [Test]
    public async Task When_AddAmanhecerHost_Should_ResolveConsumerHostedService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IMessagePumpFactory>());
        services.AddAmanhecerHost();
        using var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        await Assert.That(hostedServices.Length).IsEqualTo(1);
        await Assert.That(hostedServices[0]).IsTypeOf<ConsumerHostedService>();
    }
}
