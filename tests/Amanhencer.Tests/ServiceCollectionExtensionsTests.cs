using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;
using Amanhencer.Configurator;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Tests;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddAmanhencer_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAmanhencer();

        await Assert.That(ReferenceEquals(services, result)).IsTrue();
    }

    [Test]
    public async Task AddAmanhencer_RegistersCoreServices()
    {
        var provider = new ServiceCollection().AddAmanhencer().BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IDispatcher>()).IsTypeOf<AmanhencerDispatcher>();
        await Assert.That(provider.GetRequiredService<IHandlerFactory>()).IsTypeOf<AmanhencerHandlerFactory>();
        await Assert.That(provider.GetRequiredService<IMiddlewareFactory>()).IsTypeOf<AmanhencerMiddlewareFactory>();
        await Assert.That(provider.GetRequiredService<IPipelineFactory>()).IsTypeOf<AmanhencerPipelineFactory>();
        await Assert.That(provider.GetRequiredService<IPipelineContextFactory>()).IsTypeOf<AmanhencerPipelineContextFactory>();
        await Assert.That(provider.GetRequiredService<IExecutingStrategy>()).IsTypeOf<SequenceExecutingStrategy>();
    }

    [Test]
    public async Task AddAmanhencer_RegistersPipelineOptionsWithConfiguredRoutingKeys()
    {
        var provider = new ServiceCollection()
            .AddAmanhencer(cfg => cfg.AddRequestHandler<TestRequestHandler>())
            .BuildServiceProvider();

        var options = provider.GetRequiredService<AmanhencerPipelineOptions>();

        await Assert.That(options.Configuration.ContainsKey(typeof(TestRequest).FullName!)).IsTrue();
    }

    [Test]
    public async Task AddAmanhencer_UsesCustomExecutingStrategy()
    {
        var provider = new ServiceCollection()
            .AddAmanhencer(cfg => cfg.SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions())))
            .BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IExecutingStrategy>() is ParallelExecutingStrategy).IsTrue();
    }

    [Test]
    public async Task SendAsync_DuplicateRoutingKey_ThrowsMultiPipelineFoundException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionLog());
        services.AddAmanhencer(cfg => cfg
            .AddRoutingKey("duplicated.key", c => c.UseHandler<TestRequestHandler>())
            .AddRoutingKey("duplicated.key", c => c.UseHandler<SecondTestRequestHandler>()));
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.SendAsync(new TestRequest("hello"),
                new AmanhencerContext { RoutingKey = "duplicated.key" }))
            .ThrowsExactly<MultiPipelineFoundException>();
    }

    [Test]
    public async Task AddAmanhencer_EndToEnd_SendAndQuery()
    {
        var log = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddAmanhencer(cfg => cfg
            .AddRequestHandler<TestRequestHandler>()
            .AddQueryHandler<TestQueryHandler>());
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new TestRequest("hello"));
        var response = await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42));

        await Assert.That(log.Entries).Contains("handled:hello");
        await Assert.That(response).IsEqualTo("answer:42");
    }
}
