using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for connection failures: creating producers against an
/// unreachable broker surfaces a <see cref="BrokerUnreachableException"/>.
/// </summary>
public class RabbitMqConnectionTests
{
    [Test]
    public async Task When_The_Broker_Is_Unreachable_Should_Throw()
    {
        await using var gateway = new RabbitMqGateway
        {
            AmqpUri = new Uri("amqp://guest:guest@localhost:5999"),
            Publications =
            [
                new RabbitMqPublication
                {
                    RoutingKey = "tests",
                    RabbitMqRoutingKey = "tests",
                    Exchange = new Exchange { Name = "tests.exchange" }
                }
            ]
        };

        await Assert.That(() => gateway.CreateProducers())
            .ThrowsExactly<BrokerUnreachableException>();
    }
}
