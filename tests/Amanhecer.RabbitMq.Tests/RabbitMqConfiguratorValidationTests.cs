using System;
using System.Threading.Tasks;
using Amanhecer.RabbitMq.Configurations;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the validation performed by the RabbitMQ configurators:
/// missing required settings surface as <see cref="InvalidOperationException"/> when the
/// subscription or publication is added, and invalid arguments are rejected by guard clauses.
/// </summary>
public class RabbitMqConfiguratorValidationTests
{
    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription.QueueName("tests.queue")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Queue_Name_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription.ToRoutingKey("tests")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_That_Creates_A_Queue_Without_An_Exchange_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription
                    .ToRoutingKey("tests")
                    .QueueName("tests.queue")
                    .CreateIfNotExists(create => create.RoutingKey("tests"))))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_That_Creates_A_Queue_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionsConfigurator();

        await Assert.That(() => configurator.AddSubscription(subscription =>
                subscription
                    .ToRoutingKey("tests")
                    .QueueName("tests.queue")
                    .CreateIfNotExists(create => create.Exchange(new Exchange { Name = "tests.exchange" }))))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Publication_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqPublicationsConfigurator();

        await Assert.That(() => configurator.AddPublication(publication =>
                publication
                    .RabbitMqRoutingKey("tests")
                    .Exchange(new Exchange { Name = "tests.exchange" })))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Publication_Without_A_RabbitMq_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqPublicationsConfigurator();

        await Assert.That(() => configurator.AddPublication(publication =>
                publication
                    .RoutingKey("tests")
                    .Exchange(new Exchange { Name = "tests.exchange" })))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_Name_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.Name(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_To_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.ToRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Queue_Name_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.QueueName(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Dead_Letter_Queue_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.DeadLetterQueueRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Invalid_Message_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.InvalidMessageRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Buffer_Size_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.BufferSize(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Number_Of_Consumers_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.NumberOfConsumers(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Prefetch_Size_Should_Throw()
    {
        var configurator = new RabbitMqSubscriptionConfigurator();

        await Assert.That(() => configurator.PrefetchSize(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
