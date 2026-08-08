using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.IntegrationTests;

public class SendTests
{
    [Test]
    public async Task SendAsync_FlowsThroughMiddlewareChainInOrder_ThenHandler()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1).Use<InnerMiddleware>(order: 2)));

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Joined())
            .IsEqualTo("outer:before,inner:before,handled:apple,inner:after,outer:after");
    }

    [Test]
    public async Task Send_SyncOverload_RunsPipelineAndHandler()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        dispatcher.Send(new PlaceOrder("apple"));

        await Assert.That(log.Joined()).IsEqualTo("outer:before,handled:apple,outer:after");
    }

    [Test]
    public async Task SendAsync_WithExplicitContext_RunsPipelineAndHandler()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        await dispatcher.SendAsync(new PlaceOrder("apple"), new AmanhencerContext());

        await Assert.That(log.Joined()).IsEqualTo("outer:before,handled:apple,outer:after");
    }

    [Test]
    public async Task Send_SyncOverload_WithExplicitContext_RunsPipelineAndHandler()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        dispatcher.Send(new PlaceOrder("apple"), new AmanhencerContext());

        await Assert.That(log.Joined()).IsEqualTo("outer:before,handled:apple,outer:after");
    }
}
