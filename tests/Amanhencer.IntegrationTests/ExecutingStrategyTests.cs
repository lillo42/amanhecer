using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.IntegrationTests;

public class ExecutingStrategyTests
{
    [Test]
    public async Task DefaultStrategy_IsSequential_AndPipelinesRunInRegistrationOrder()
    {
        var log = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddAmanhencer(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });
        var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IExecutingStrategy>() is SequenceExecutingStrategy).IsTrue();

        await provider.GetRequiredService<IDispatcher>().PublishAsync(new OrderShipped("book"));

        await Assert.That(log.Joined()).IsEqualTo("first:book,second:book");
    }

    [Test]
    public async Task SetExecutorStrategy_ParallelStrategy_RunsAllPipelines()
    {
        var log = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddAmanhencer(cfg =>
        {
            cfg.SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions()));
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });
        var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IExecutingStrategy>() is ParallelExecutingStrategy).IsTrue();

        await provider.GetRequiredService<IDispatcher>().PublishAsync(new OrderShipped("book"));

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }

    [Test]
    public async Task ContextExecutingStrategy_OverridesDefaultPerDispatch()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });
        var context = new AmanhencerContext
        {
            ExecutingStrategy = new ParallelExecutingStrategy(new ParallelOptions())
        };

        await dispatcher.PublishAsync(new OrderShipped("book"), context);

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }
}
