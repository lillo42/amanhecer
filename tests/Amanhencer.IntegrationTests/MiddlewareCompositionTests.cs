using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Amanhencer.IntegrationTests;

public class MiddlewareCompositionTests
{
    [Test]
    public async Task ConfiguratorMiddlewares_WrapHandlerOutermostFirst()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing => routing
            .Use<InnerMiddleware>(order: 3)
            .Use<OuterMiddleware>(order: 1)
            .Use<MiddleMiddleware>(order: 2)));

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Joined())
            .IsEqualTo("outer:before,middle:before,inner:before,handled:apple,inner:after,middle:after,outer:after");
    }

    [Test]
    public async Task AttributeMiddlewares_WrapHandlerInDeclaredOrder()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<AttributedOrderHandler>());

        await dispatcher.SendAsync(new AttributedOrder("apple"));

        await Assert.That(log.Joined())
            .IsEqualTo("audit:before,timing:before,handled:apple,timing:after,audit:after");
    }

    [Test]
    public async Task AttributeAndConfiguratorMiddlewares_CombineInOrder()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<AttributedOrderHandler>(routing =>
            routing.Use<OuterMiddleware>(order: 0)));

        await dispatcher.SendAsync(new AttributedOrder("apple"));

        await Assert.That(log.Joined())
            .IsEqualTo("outer:before,audit:before,timing:before,handled:apple,timing:after,audit:after,outer:after");
    }

    [Test]
    public async Task Middleware_CanShortCircuitThePipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing => routing
            .Use<ShortCircuitMiddleware>(order: 1)
            .Use<OuterMiddleware>(order: 2)));

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Joined()).IsEqualTo("short-circuit");
    }
}
