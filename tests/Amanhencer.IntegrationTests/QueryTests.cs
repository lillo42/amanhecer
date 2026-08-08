using System.Threading.Tasks;
using Amanhencer.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Amanhencer.IntegrationTests;

public class QueryTests
{
    [Test]
    public async Task QueryAsync_ResponseFlowsBackThroughPipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        var stock = await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple"));

        await Assert.That(stock).IsEqualTo(5);
        await Assert.That(log.Joined()).IsEqualTo("outer:before,queried:apple,outer:after");
    }

    [Test]
    public async Task Query_SyncOverload_ReturnsHandlerResponse()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 1)));

        var stock = dispatcher.Query<GetStock, int>(new GetStock("apple"));

        await Assert.That(stock).IsEqualTo(5);
        await Assert.That(log.Joined()).IsEqualTo("outer:before,queried:apple,outer:after");
    }

    [Test]
    public async Task QueryAsync_WithExplicitContext_ReturnsHandlerResponse()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>());

        var stock = await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple"), new AmanhencerContext());

        await Assert.That(stock).IsEqualTo(5);
    }

    [Test]
    public async Task Query_SyncOverload_WithExplicitContext_ReturnsHandlerResponse()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<GetStockHandler>());

        var stock = dispatcher.Query<GetStock, int>(new GetStock("apple"), new AmanhencerContext());

        await Assert.That(stock).IsEqualTo(5);
    }
}
