using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-independent tests for <see cref="RabbitMqStreamGateway"/>: its default connection
/// settings and the argument validation performed before a stream system is created.
/// </summary>
public class RabbitMqStreamGatewayTests
{
    [Test]
    public async Task When_Created_Should_Default_To_The_Guest_Credentials_And_Local_Endpoint()
    {
        var gateway = new RabbitMqStreamGateway();

        await Assert.That(gateway.UserName).IsEqualTo("guest");
        await Assert.That(gateway.Password).IsEqualTo("guest");
        await Assert.That(gateway.VirtualHost).IsEqualTo("/");
        await Assert.That(gateway.EndPoints.Count).IsEqualTo(1);
    }

    [Test]
    public async Task When_Creating_A_Consumer_For_A_Non_Stream_Subscription_Should_Throw()
    {
        var gateway = new RabbitMqStreamGateway();

        await Assert
            .That(() => gateway.CreateConsumer(new TestSubscription("tests")))
            .ThrowsExactly<System.ArgumentException>();
    }

    private sealed class TestSubscription(string toRoutingKey) : Subscription(toRoutingKey);
}
