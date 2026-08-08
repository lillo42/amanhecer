using System.Threading.Tasks;
using Amanhencer.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Amanhencer.IntegrationTests;

public class PublishTests
{
    [Test]
    public async Task PublishAsync_MultipleHandlers_EachRunsThroughItsOwnPipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing
                .Use<OuterMiddleware>(order: 1)
                .UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing
                .Use<InnerMiddleware>(order: 1)
                .UseHandler<SecondShippedHandler>());
        });

        await dispatcher.PublishAsync(new OrderShipped("book"));

        await Assert.That(log.Joined())
            .IsEqualTo("outer:before,first:book,outer:after,inner:before,second:book,inner:after");
    }

    [Test]
    public async Task Publish_SyncOverload_RunsAllHandlers()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });

        dispatcher.Publish(new OrderShipped("book"));

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }

    [Test]
    public async Task PublishAsync_WithExplicitContext_RunsAllHandlers()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });

        await dispatcher.PublishAsync(new OrderShipped("book"), new AmanhencerContext());

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }

    [Test]
    public async Task PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        var (dispatcher, log) = DispatcherFixture.Create(_ => { });

        await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book")))
            .ThrowsNothing();
        await Assert.That(log.Entries).IsEmpty();
    }
}
