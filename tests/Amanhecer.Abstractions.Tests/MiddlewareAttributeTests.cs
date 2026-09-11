using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Tests;

public class MiddlewareAttributeTests
{
    [Test]
    public async Task Constructor_Should_SetOrder()
    {
        var attribute = new TestMiddlewareAttribute(order: 3);

        await Assert.That(attribute.Order).IsEqualTo(3);
    }

    [Test]
    public async Task Order_Should_BeSettable()
    {
        var attribute = new TestMiddlewareAttribute(order: 3) { Order = 10 };

        await Assert.That(attribute.Order).IsEqualTo(10);
    }

    [Test]
    public async Task GetMiddlewareType_Should_ReturnMiddlewareType()
    {
        var attribute = new TestMiddlewareAttribute(order: 0);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(FakeMiddleware));
    }

    [Test]
    public async Task AttributeUsage_Should_TargetMethodsAndClasses()
    {
        var usage = typeof(MiddlewareAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Method | AttributeTargets.Class);
    }

    private class TestMiddlewareAttribute(int order) : MiddlewareAttribute<FakeMiddleware>(order);

    private class FakeMiddleware : IMiddleware
    {
        public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
            => next(context);
    }
}
