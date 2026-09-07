using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Tests;

public class RoutingKeyAttributeTests
{
    [Test]
    public async Task Constructor_Should_StoreRoutingKey()
    {
        var attribute = new RoutingKeyAttribute("orders.created");

        await Assert.That(attribute.RoutingKey).IsEqualTo("orders.created");
    }

    [Test]
    public async Task Attribute_Should_BeRetrievableFromAnnotatedType()
    {
        var attribute = typeof(AnnotatedRequest).GetCustomAttribute<RoutingKeyAttribute>();

        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.RoutingKey).IsEqualTo("annotated.key");
    }

    [Test]
    public async Task AttributeUsage_Should_TargetClassesAndStructsOnly()
    {
        var usage = typeof(RoutingKeyAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Class | AttributeTargets.Struct);
    }

    [RoutingKey("annotated.key")]
    private class AnnotatedRequest;
}
