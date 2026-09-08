using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using NSubstitute;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the RabbitMQ transport's argument validation.
/// </summary>
public class RabbitMqValidationTests
{
    [Test]
    public async Task When_Producing_Through_A_Non_RabbitMq_Publication_Should_Throw()
    {
        var channel = Substitute.For<IChannel>();
        var producer = new RabbitMqProducer(channel);

        await Assert.That(async () => await producer.ProduceAsync(
                new Message(),
                new TestPublication { RoutingKey = "tests" },
                new AmanhecerContext()))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Creating_A_Consumer_For_A_Non_RabbitMq_Subscription_Should_Throw()
    {
        var gateway = new RabbitMqGateway();

        await Assert.That(() => gateway.CreateConsumer(new TestSubscription("tests")))
            .ThrowsExactly<ArgumentException>();
    }

    private sealed class TestPublication : Publication;

    private sealed class TestSubscription(string toRoutingKey) : Subscription(toRoutingKey);
}
