using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.IntegrationTests;

public class ErrorAndCancellationTests
{
    [Test]
    public async Task SendAsync_NoPipelineRegistered_ThrowsPipelineNotFoundException()
    {
        var (dispatcher, _) = DispatcherFixture.Create(_ => { });

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple")))
            .ThrowsExactly<PipelineNotFoundException>();
    }

    [Test]
    public async Task QueryAsync_NoPipelineRegistered_ThrowsPipelineNotFoundException()
    {
        var (dispatcher, _) = DispatcherFixture.Create(_ => { });

        await Assert.That(async () => await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple")))
            .ThrowsExactly<PipelineNotFoundException>();
    }

    [Test]
    public async Task SendAsync_MultiplePipelinesForKey_ThrowsMultiPipelineFoundException()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<PlaceOrder>(), routing => routing.UseHandler<PlaceOrderHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<PlaceOrder>(), routing => routing.UseHandler<PlaceOrderHandler>());
        });

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple")))
            .ThrowsExactly<MultiPipelineFoundException>();
    }

    [Test]
    public async Task QueryAsync_MultiplePipelinesForKey_ThrowsMultiPipelineFoundException()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<GetStock>(), routing => routing.UseHandler<GetStockHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<GetStock>(), routing => routing.UseHandler<GetStockHandler>());
        });

        await Assert.That(async () => await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple")))
            .ThrowsExactly<MultiPipelineFoundException>();
    }

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

        // Deliberate contract: AmanhencerPipeline treats a cancelled token as a silent no-op
        // (it returns early instead of throwing OperationCanceledException), so the dispatch
        // completes successfully without running anything.
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

        // Deliberate contract, as above: pre-cancelled token means silent no-op, not
        // OperationCanceledException.
        await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book"), cts.Token))
            .ThrowsNothing();
        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task SendAsync_TokenCancelledMidPipeline_SkipsRemainingMiddlewaresSilently()
    {
        var log = new ExecutionLog();
        using var cts = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddSingleton(cts);
        services.AddAmanhencer(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing => routing
            .Use<CancellingMiddleware>(order: 1)
            .Use<OuterMiddleware>(order: 2)));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IDispatcher>();

        // Pinned, but questionable: the pipeline only observes cancellation between middlewares
        // and then silently abandons the rest of the chain — no OperationCanceledException is
        // thrown, so a token cancelled while the pipeline runs turns the dispatch into a
        // partial, silent no-op.
        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple"), cts.Token))
            .ThrowsNothing();
        await Assert.That(log.Joined()).IsEqualTo("cancelling:before,cancelling:after");
    }

    [Test]
    public async Task QueryAsync_CancelledToken_ThrowsNullReferenceException()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Pinned, but questionable: the cancelled token silently skips the pipeline (no
        // OperationCanceledException), the handler never runs, Response stays null and the
        // dispatcher's unboxing of that null response surfaces as a NullReferenceException.
        await Assert.That(async () => await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple"), cts.Token))
            .ThrowsExactly<NullReferenceException>();
        await Assert.That(log.Entries).IsEmpty();
    }
}
