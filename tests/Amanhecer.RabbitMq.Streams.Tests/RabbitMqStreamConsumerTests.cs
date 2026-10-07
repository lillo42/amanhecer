using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-independent tests for <see cref="RabbitMqStreamConsumer"/>: the subscription it exposes
/// and the settle operations that degrade gracefully when the required offset metadata is missing.
/// </summary>
public class RabbitMqStreamConsumerTests
{
    [Test]
    public async Task When_Created_Should_Expose_The_Subscription()
    {
        var subscription = new RabbitMqStreamSubscription("tests", "tests.stream");
        var consumer = new RabbitMqStreamConsumer(null!, subscription);

        await Assert.That(consumer.Subscription).IsSameReferenceAs(subscription);
    }

    [Test]
    public async Task When_Acknowledging_A_Message_Without_Offset_Metadata_Should_Not_Throw()
    {
        var consumer = CreateConsumer();

        await Assert
            .That(async () => await consumer.AckAsync(new Message()))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Nacking_A_Message_Should_Fall_Back_To_Acknowledging_Without_Throwing()
    {
        var consumer = CreateConsumer();

        await Assert
            .That(async () => await consumer.NackAsync(new Message()))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Deferring_A_Message_Should_Fall_Back_To_Acknowledging_Without_Throwing()
    {
        var consumer = CreateConsumer();

        await Assert
            .That(async () => await consumer.DeferAsync(new Message(), TimeSpan.FromSeconds(1)))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Disposing_Before_Initialization_Should_Not_Throw()
    {
        var consumer = CreateConsumer();

        await Assert
            .That(async () => await consumer.DisposeAsync())
            .ThrowsNothing();
    }

    private static RabbitMqStreamConsumer CreateConsumer()
    {
        var subscription = new RabbitMqStreamSubscription("tests", "tests.stream");
        return new RabbitMqStreamConsumer(null!, subscription);
    }
}
