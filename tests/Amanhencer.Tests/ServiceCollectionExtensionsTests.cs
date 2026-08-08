using System.Threading.Tasks;
using Amanhencer.Abstractions;
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

        await Assert.That(provider.GetRequiredService<IDispatcher>() is AmanhencerDispatcher).IsTrue();
        await Assert.That(provider.GetRequiredService<IHandlerFactory>() is AmanhencerHandlerFactory).IsTrue();
        await Assert.That(provider.GetRequiredService<IMiddlewareFactory>() is AmanhencerMiddlewareFactory).IsTrue();
        await Assert.That(provider.GetRequiredService<IPipelineFactory>() is AmanhencerPipelineFactory).IsTrue();
        await Assert.That(provider.GetRequiredService<IPipelineContextFactory>() is AmanhencerPipelineContextFactory).IsTrue();
        await Assert.That(provider.GetRequiredService<IExecutingStrategy>() is SequenceExecutingStrategy).IsTrue();
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
