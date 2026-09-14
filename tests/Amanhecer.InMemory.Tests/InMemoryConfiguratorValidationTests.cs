using System;
using System.Reflection;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.InMemory.Configurations;

namespace Amanhecer.InMemory.Tests;

public class InMemoryConfiguratorValidationTests
{
    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new InMemorySubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription.QueueName("tests.queue")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Queue_Name_Should_Throw()
    {
        var configurator = new InMemorySubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription.ToRoutingKey("tests")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Publication_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new InMemoryPublicationsConfigurator();

        await Assert.That(() => configurator.AddPublication(publication =>
                publication.QueueName("tests.queue")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_Name_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.Name(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_To_Routing_Key_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.ToRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Queue_Name_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.QueueName(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Dead_Letter_Queue_Routing_Key_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.DeadLetterQueueRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Invalid_Message_Routing_Key_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.InvalidMessageRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Buffer_Size_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.BufferSize(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Number_Of_Consumers_Should_Throw()
    {
        var configurator = new InMemorySubscriptionConfigurator();

        await Assert.That(() => configurator.NumberOfConsumers(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_DefaultMessageMapper_WithNonMapperType_Should_ThrowArgumentException()
    {
        var configurator = new InMemoryConfigurator();

        await Assert.That(() => configurator.DefaultMessageMapper(typeof(string)))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Publication_Does_Not_Set_QueueName_Should_Default_To_RoutingKey()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing");

        var publication = CreatePublication(cfg);

        await Assert.That(publication.QueueName).IsEqualTo("tests.routing");
    }

    private static InMemoryPublication CreatePublication(InMemoryPublicationConfigurator cfg)
    {
        var toPublication = typeof(InMemoryPublicationConfigurator)
            .GetMethod("ToPublication", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (InMemoryPublication)toPublication.Invoke(cfg, null)!;
    }
}
