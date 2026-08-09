using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;

// AutoFromAssemblies is intentionally trim-unsafe (RequiresUnreferencedCode);
// these tests exercise it directly, so the trim warning does not apply here.
#pragma warning disable IL2026

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

    [Test]
    public async Task AutoFromAssemblies_RegistersHandlersWithRoutingKeys()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AutoFromAssemblies(typeof(TestRequestHandler).Assembly);

        var keys = configurator.RoutingConfigurators.Select(r => r.RoutingKey).ToArray();
        await Assert.That(keys).Contains("routed.request");
        await Assert.That(keys).Contains(typeof(TestRequest).FullName!);
        await Assert.That(keys).Contains(typeof(TestQuery).FullName!);
    }

    [Test]
    public async Task AutoFromAssemblies_RegistersMiddlewareTypesInServices()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        configurator.AutoFromAssemblies(typeof(TestRequestHandler).Assembly);

        await Assert.That(services.Any(d => d.ServiceType == typeof(FirstMiddleware))).IsTrue();
        await Assert.That(services.Any(d => d.ServiceType == typeof(PassThroughMiddleware))).IsTrue();
    }

    [Test]
    public async Task AutoFromAssemblies_SkipsAbstractAndOpenGenericTypes()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        configurator.AutoFromAssemblies(typeof(TestRequestHandler).Assembly);

        var keys = configurator.RoutingConfigurators.Select(r => r.RoutingKey).ToArray();
        await Assert.That(keys.Any(k => k == typeof(SkippedRequest).FullName)).IsFalse();
        await Assert.That(keys.Any(k => k.Contains("GenericRequest"))).IsFalse();
        await Assert.That(services.Any(d => d.ServiceType == typeof(OpenGenericMiddleware<>))).IsFalse();
    }

    [Test]
    public async Task AutoFromAssemblies_ScanningSameAssemblyTwice_RegistersHandlersOnce()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AutoFromAssemblies(typeof(RoutedRequestHandler).Assembly);
        configurator.AutoFromAssemblies(typeof(RoutedRequestHandler).Assembly);

        var keys = configurator.RoutingConfigurators.Select(r => r.RoutingKey).ToArray();
        await Assert.That(keys.Count(k => k == "routed.request")).IsEqualTo(1);
    }

    [Test]
    public async Task AutoFromAssemblies_WithoutAssemblies_ScansCallingAssembly()
    {
        var configurator = new AmanhencerConfigurator(new ServiceCollection());

        configurator.AutoFromAssemblies();

        var keys = configurator.RoutingConfigurators.Select(r => r.RoutingKey).ToArray();
        await Assert.That(keys).Contains("routed.request");
    }
}
