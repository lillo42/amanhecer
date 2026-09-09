using System.Threading.Tasks;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the <see cref="RabbitMqSubscription"/> defaults.
/// </summary>
public class RabbitMqSubscriptionTests
{
    [Test]
    public async Task When_Creating_A_Subscription_Should_Default_To_The_RabbitMq_Messaging_System()
    {
        var subscription = new RabbitMqSubscription("some.routing.key", "some-queue");

        await Assert.That(subscription.MessagingSystem).IsEqualTo("rabbitmq");
    }
}
