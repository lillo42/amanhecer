using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Metadatas;
using Amanhecer.Configurator;
using Amanhecer.Middlewares;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Tests.Configurator;

public class AmanhecerRoutingConfiguratorTests
{
    [Test]
    public async Task When_ToOptions_Should_AppendExecuteHandlerMiddleware()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var last = options.Middlewares.Last();
        await Assert.That(last)
            .Member(x => x.MiddlewareType, y => y.IsEqualTo(typeof(ExecuteHandlerMiddleware)))
            .And.Member(x => x.Order, y => y.IsEqualTo(int.MaxValue))
            .And.Member(x => x.Metadata, y => y.IsEqualTo(new HandleTypeMetadata(typeof(SomeRequestHandler))));
    }

    [Test]
    public async Task When_ToOptions_Should_KeepRoutingKey()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        var options = configurator.ToOptions();

        await Assert.That(options.RoutingKey).IsEqualTo("some.key");
    }

    [Test]
    public async Task When_ToOptions_Should_OrderMiddlewaresByOrder()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.Use<AnotherMiddleware>(order: 10);
        configurator.Use<SomeMiddleware>(order: -5);
        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var middlewares = options.Middlewares.ToArray();
        await Assert.That(middlewares).Count().IsEqualTo(3);
        await Assert.That(middlewares[0])
            .Member(x => x.MiddlewareType, y => y.IsEqualTo(typeof(SomeMiddleware)))
            .And.Member(x => x.Order, y => y.IsEqualTo(-5));
        await Assert.That(middlewares[1])
            .Member(x => x.MiddlewareType, y => y.IsEqualTo(typeof(AnotherMiddleware)))
            .And.Member(x => x.Order, y => y.IsEqualTo(10));
        await Assert.That(middlewares[2])
            .Member(x => x.MiddlewareType, y => y.IsEqualTo(typeof(ExecuteHandlerMiddleware)))
            .And.Member(x => x.Order, y => y.IsEqualTo(int.MaxValue));
    }

    [Test]
    public async Task When_Use_WithType_Should_StoreMiddlewareOptions()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.Use(typeof(SomeMiddleware), order: 3, metadata: "some-metadata");
        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var middleware = options.Middlewares
            .Single(x => x.MiddlewareType == typeof(SomeMiddleware));
        await Assert.That(middleware)
            .Member(x => x.Order, y => y.IsEqualTo(3))
            .And.Member(x => x.Metadata, y => y.IsEqualTo("some-metadata"));
    }

    [Test]
    public async Task When_Use_WithType_Should_RegisterMiddlewareAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.Use(typeof(SomeMiddleware));

        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(SomeMiddleware) &&
                           x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_UseHandler_WithType_Should_RegisterHandlerAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.UseHandler(typeof(SomeRequestHandler));

        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(SomeRequestHandler) &&
                           x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_UseHandler_WithType_Should_SetHandlerMetadata()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerRoutingConfigurator("some.key", services);

        configurator.UseHandler(typeof(SomeRequestHandler));
        var options = configurator.ToOptions();

        var last = options.Middlewares.Last();
        await Assert.That(last)
            .Member(x => x.MiddlewareType, y => y.IsEqualTo(typeof(ExecuteHandlerMiddleware)))
            .And.Member(x => x.Metadata, y => y.IsEqualTo(new HandleTypeMetadata(typeof(SomeRequestHandler))));
    }

    private class SomeMiddleware : IMiddleware
    {
        public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
            => next(context);
    }

    private class AnotherMiddleware : IMiddleware
    {
        public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
            => next(context);
    }

    private record SomeRequest;

    private class SomeRequestHandler : RequestHandler<SomeRequest>
    {
        public override ValueTask HandleAsync(SomeRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
