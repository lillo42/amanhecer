using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Tests.Configurator;

public class AmanhencerRoutingConfiguratorTests
{
    [Test]
    public async Task When_ToOptions_Should_AppendExecuteHandlerMiddleware()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var last = options.MiddlewareOptions.Last();
        await Assert.That(last.MiddlewareType).IsEqualTo(typeof(ExecuteHandlerMiddleware));
        await Assert.That(last.Order).IsEqualTo(int.MaxValue);
        await Assert.That(last.Metadata).IsEqualTo(typeof(SomeRequestHandler));
    }

    [Test]
    public async Task When_ToOptions_Should_KeepRoutingKey()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        var options = configurator.ToOptions();

        await Assert.That(options.RoutingKey).IsEqualTo("some.key");
    }

    [Test]
    public async Task When_ToOptions_Should_OrderMiddlewaresByOrder()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.Use<AnotherMiddleware>(order: 10);
        configurator.Use<SomeMiddleware>(order: -5);
        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var middlewares = options.MiddlewareOptions.ToArray();
        await Assert.That(middlewares.Length).IsEqualTo(3);
        await Assert.That(middlewares[0].MiddlewareType).IsEqualTo(typeof(SomeMiddleware));
        await Assert.That(middlewares[0].Order).IsEqualTo(-5);
        await Assert.That(middlewares[1].MiddlewareType).IsEqualTo(typeof(AnotherMiddleware));
        await Assert.That(middlewares[1].Order).IsEqualTo(10);
        await Assert.That(middlewares[2].MiddlewareType).IsEqualTo(typeof(ExecuteHandlerMiddleware));
        await Assert.That(middlewares[2].Order).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task When_Use_WithType_Should_StoreMiddlewareOptions()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.Use(typeof(SomeMiddleware), order: 3, metadata: "some-metadata");
        configurator.UseHandler<SomeRequestHandler>();
        var options = configurator.ToOptions();

        var middleware = options.MiddlewareOptions
            .Single(x => x.MiddlewareType == typeof(SomeMiddleware));
        await Assert.That(middleware.Order).IsEqualTo(3);
        await Assert.That(middleware.Metadata).IsEqualTo("some-metadata");
    }

    [Test]
    public async Task When_Use_WithType_Should_RegisterMiddlewareAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.Use(typeof(SomeMiddleware));

        await Assert.That(services.Any(x => x.ServiceType == typeof(SomeMiddleware) &&
                                            x.Lifetime == ServiceLifetime.Transient))
            .IsTrue();
    }

    [Test]
    public async Task When_UseHandler_WithType_Should_RegisterHandlerAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.UseHandler(typeof(SomeRequestHandler));

        await Assert.That(services.Any(x => x.ServiceType == typeof(SomeRequestHandler) &&
                                            x.Lifetime == ServiceLifetime.Transient))
            .IsTrue();
    }

    [Test]
    public async Task When_UseHandler_WithType_Should_SetHandlerMetadata()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerRoutingConfigurator("some.key", services);

        configurator.UseHandler(typeof(SomeRequestHandler));
        var options = configurator.ToOptions();

        var last = options.MiddlewareOptions.Last();
        await Assert.That(last.MiddlewareType).IsEqualTo(typeof(ExecuteHandlerMiddleware));
        await Assert.That(last.Metadata).IsEqualTo(typeof(SomeRequestHandler));
    }

    private class SomeMiddleware : IMiddleware
    {
        public void Initialize(object? metadata)
        {
        }

        public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
            => next(context);
    }

    private class AnotherMiddleware : IMiddleware
    {
        public void Initialize(object? metadata)
        {
        }

        public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
            => next(context);
    }

    private record SomeRequest;

    private class SomeRequestHandler : RequestHandler<SomeRequest>
    {
        public override ValueTask HandleAsync(SomeRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
