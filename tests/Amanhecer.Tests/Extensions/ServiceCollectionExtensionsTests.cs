using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
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

    [Test]
    public async Task When_AddAmanhecer_Should_NotCreateProducersUntilTheProducerFinderIsResolved()
    {
        var producer = Substitute.For<IProducer>();
        var gateway = Substitute.For<IGateway>();
        gateway.Name.Returns("test-gateway");
        gateway.Publications.Returns([]);
        gateway.Subscriptions.Returns([]);
        gateway.CreateProducers().Returns(new Dictionary<string, IProducer> { ["test"] = producer });

        var services = new ServiceCollection();
        services.AddAmanhecer(a => a.UsingMessagingGateway(m => m.AddGateway(gateway)));

        gateway.DidNotReceive().CreateProducers();

        var finder = services.BuildServiceProvider().GetRequiredService<IProducerFinder>();

        gateway.Received(1).CreateProducers();
        await Assert.That(finder.Find("test")).IsSameReferenceAs(producer);
    }

    [Test]
    public async Task When_AddAmanhecer_WithDuplicatedPublicationRoutingKeyAcrossGateways_Should_ThrowInvalidOperationException()
    {
        var gatewayA = CreateGateway("gateway-a", CreatePublication("publication-a", "shared-key"));
        var gatewayB = CreateGateway("gateway-b", CreatePublication("publication-b", "shared-key"));

        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhecer(a => a.UsingMessagingGateway(m =>
                {
                    m.AddGateway(gatewayA);
                    m.AddGateway(gatewayB);
                })))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("shared-key");
    }

    [Test]
    public async Task When_AddAmanhecer_WithDistinctPublicationRoutingKeysAcrossGateways_Should_NotThrow()
    {
        var gatewayA = CreateGateway("gateway-a", CreatePublication("publication-a", "key-a"));
        var gatewayB = CreateGateway("gateway-b", CreatePublication("publication-b", "key-b"));

        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhecer(a => a.UsingMessagingGateway(m =>
                {
                    m.AddGateway(gatewayA);
                    m.AddGateway(gatewayB);
                })))
            .ThrowsNothing();
    }

    private static IGateway CreateGateway(string name, params IPublication[] publications)
    {
        var gateway = Substitute.For<IGateway>();
        gateway.Name.Returns(name);
        gateway.Publications.Returns(publications);
        gateway.Subscriptions.Returns([]);
        return gateway;
    }

    private static IPublication CreatePublication(string name, string routingKey)
    {
        var publication = Substitute.For<IPublication>();
        publication.Name.Returns(name);
        publication.RoutingKey.Returns(routingKey);
        publication.Transformers.Returns([]);
        return publication;
    }
}
