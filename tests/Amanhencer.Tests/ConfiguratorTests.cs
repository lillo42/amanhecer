using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Tests;

public class ConfiguratorTests
{
    [Test]
    public async Task AddRequestHandler_UsesRequestTypeFullNameAsRoutingKey()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AddRequestHandler<TestRequestHandler>();

        var routing = configurator.RoutingConfigurators.Single();
        await Assert.That(routing.RoutingKey).IsEqualTo(typeof(TestRequest).FullName!);
    }

    [Test]
    public async Task AddRequestHandler_UsesRoutingKeyAttribute()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AddRequestHandler<RoutedRequestHandler>();

        var routing = configurator.RoutingConfigurators.Single();
        await Assert.That(routing.RoutingKey).IsEqualTo("routed.request");
    }

    [Test]
    public async Task AddQueryHandler_UsesQueryTypeFullNameAsRoutingKey()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AddQueryHandler<TestQueryHandler>();

        var routing = configurator.RoutingConfigurators.Single();
        await Assert.That(routing.RoutingKey).IsEqualTo(typeof(TestQuery).FullName!);
    }

    [Test]
    public async Task AddRoutingKey_UsesExplicitRoutingKey_AndReturnsSameConfigurator()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        var result = configurator.AddRoutingKey("custom.key");

        await Assert.That(ReferenceEquals(configurator, result)).IsTrue();
        var routing = configurator.RoutingConfigurators.Single();
        await Assert.That(routing.RoutingKey).IsEqualTo("custom.key");
    }

    [Test]
    public async Task RoutingConfigurator_OrdersMiddlewaresByOrder_AndAppendsHandlerMiddlewareLast()
    {
        var services = new ServiceCollection();
        var routing = new AmanhencerRoutingConfigurator("test.key", services);

        var options = routing
            .Use<SecondMiddleware>(order: 2)
            .Use<FirstMiddleware>(order: 1)
            .UseHandler<TestRequestHandler>()
            .ToOptions();

        var middlewares = options.MiddlewareOptions.ToArray();
        await Assert.That(middlewares.Length).IsEqualTo(3);
        await Assert.That(middlewares[0].MiddlewareType).IsEqualTo(typeof(FirstMiddleware));
        await Assert.That(middlewares[1].MiddlewareType).IsEqualTo(typeof(SecondMiddleware));
        await Assert.That(middlewares[2].MiddlewareType).IsEqualTo(typeof(ExecuteHandlerMiddleware));
        await Assert.That(middlewares[2].Metadata).IsEqualTo(typeof(TestRequestHandler));
    }

    [Test]
    public async Task UseHandler_RegistersMiddlewaresDeclaredByAttributes()
    {
        var services = new ServiceCollection();
        var routing = new AmanhencerRoutingConfigurator("test.key", services);

        var options = routing.UseHandler<AttributedRequestHandler>().ToOptions();

        var middlewareTypes = options.MiddlewareOptions.Select(m => m.MiddlewareType).ToArray();
        await Assert.That(middlewareTypes).Contains(typeof(FirstMiddleware));
        await Assert.That(middlewareTypes).Contains(typeof(SecondMiddleware));
        await Assert.That(middlewareTypes[^1]).IsEqualTo(typeof(ExecuteHandlerMiddleware));
    }

    [Test]
    public async Task Use_RegistersMiddlewareTypeInServices()
    {
        var services = new ServiceCollection();
        var routing = new AmanhencerRoutingConfigurator("test.key", services);

        routing.Use<FirstMiddleware>();

        await Assert.That(services.Any(d => d.ServiceType == typeof(FirstMiddleware))).IsTrue();
    }

    [Test]
    public async Task UseHandler_RegistersHandlerTypeInServices()
    {
        var services = new ServiceCollection();
        var routing = new AmanhencerRoutingConfigurator("test.key", services);

        routing.UseHandler<TestRequestHandler>();

        await Assert.That(services.Any(d => d.ServiceType == typeof(TestRequestHandler))).IsTrue();
    }

    [Test]
    public async Task SetExecutorStrategy_RegistersStrategyAsSingleton()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        configurator.SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions()));

        var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IExecutingStrategy>() is ParallelExecutingStrategy).IsTrue();
    }
}
