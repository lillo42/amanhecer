using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.IntegrationTests;

public class ErrorAndCancellationTests
{
    [Test]
    public async Task SendAsync_HandlerException_PropagatesOutOfDispatcher()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<ExplodingOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Send_HandlerException_PropagatesOutOfSyncOverload()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<ExplodingOrderHandler>());

        await Assert.That(() => dispatcher.Send(new PlaceOrder("apple")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task QueryAsync_HandlerException_PropagatesOutOfDispatcher()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>(routing =>
            routing.Use<ExplodingMiddleware>(order: 1)));

        await Assert.That(async () => await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_MiddlewareException_PropagatesAndHandlerDoesNotRun()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<ExplodingMiddleware>(order: 1)));

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple")))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task SendAsync_CancelledToken_SkipsPipelineExecution()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple"), cts.Token))
            .ThrowsNothing();
        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task PublishAsync_CancelledToken_SkipsPipelineExecution()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book"), cts.Token))
            .ThrowsNothing();
        await Assert.That(log.Entries).IsEmpty();
    }
}
