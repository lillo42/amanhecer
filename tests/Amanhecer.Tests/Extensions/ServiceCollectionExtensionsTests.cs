using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.ExecutingStrategies;
using Amanhecer.Extensions;
using Amanhecer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task When_AddAmanhecer_WithCustomDispatcher_Should_KeepCustomRegistration()
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var services = new ServiceCollection();
        services.AddSingleton(dispatcher);

        services.AddAmanhecer();

        await Assert.That(services.Count(x => x.ServiceType == typeof(IDispatcher)))
            .IsEqualTo(1);

        var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IDispatcher>())
            .IsSameReferenceAs(dispatcher);
    }

    [Test]
    public async Task When_AddAmanhecer_WithCustomExecutingStrategy_Should_KeepCustomRegistration()
    {
        var strategy = Substitute.For<IExecutingStrategy>();
        var services = new ServiceCollection();
        services.AddSingleton(strategy);

        services.AddAmanhecer();

        await Assert.That(services.Count(x => x.ServiceType == typeof(IExecutingStrategy)))
            .IsEqualTo(1);

        var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IExecutingStrategy>())
            .IsSameReferenceAs(strategy);
    }

    [Test]
    public async Task When_AddAmanhecer_Should_RegisterSequenceExecutingStrategyAsDefault()
    {
        var services = new ServiceCollection();

        services.AddAmanhecer();

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(IExecutingStrategy));
        await Assert.That(descriptor).IsNotNull()
            .And.Member(x => x.Lifetime, y => y.IsEqualTo(ServiceLifetime.Singleton))
            .And.Member(x => x.ImplementationType, y => y.IsEqualTo(typeof(SequenceExecutingStrategy)));
    }

    [Test]
    public async Task When_AddAmanhecer_Should_RegisterTelemetryMiddlewareAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAmanhecer();

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(AmanhecerTelemetryMiddleware));
        await Assert.That(descriptor).IsNotNull()
            .And.Member(x => x.Lifetime, y => y.IsEqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public async Task When_AddAmanhecer_Should_RegisterLoggerMiddlewareAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAmanhecer();

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(AmanhecerLoggerMiddleware));
        
        await Assert.That(descriptor).IsNotNull()
            .And.Member(x => x.Lifetime, y => y.IsEqualTo(ServiceLifetime.Singleton));
    }
}
