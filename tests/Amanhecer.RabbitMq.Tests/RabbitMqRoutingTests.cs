using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Tests for topic routing: messages whose routing key does not match the queue binding are
/// not received, and received messages carry the exchange and routing key they were
/// published with.
/// Requires a broker (see docker-compose-rabbitmq.yaml at the repository root).
/// </summary>
public class RabbitMqRoutingTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(500);

    [Test]
    public async Task When_Producing_With_A_Non_Matching_Routing_Key_Should_Not_Be_Received()
    {
        await using var fixture = await RabbitMqMessagingGatewayFixture.CreateAsync(
            configureQueue: queue => queue.RoutingKey += ".#",
            publicationRoutingKey: $"unrouted.{Uuid.NewGuid():N}");
        var message = RabbitMqMessagingGatewayFixture.CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        await Assert.That(async () =>
                await RabbitMqMessagingGatewayFixture.ReceiveOneAsync(fixture, NoMessageTimeout))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task When_Receiving_A_Message_Should_Stamp_Routing_Metadata()
    {
        await using var fixture = await RabbitMqMessagingGatewayFixture.CreateAsync();
        var publication = (RabbitMqPublication)fixture.Publication;
        var message = RabbitMqMessagingGatewayFixture.CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await RabbitMqMessagingGatewayFixture.ReceiveOneAsync(fixture, ReceiveTimeout);

        await Assert.That(received.Metadata[MetadataName.Exchange])
            .IsEqualTo(publication.Exchange!.Name);
        await Assert.That(received.Metadata[MetadataName.RoutingKey])
            .IsEqualTo(publication.RabbitMqRoutingKey);

        await fixture.Consumer.AckAsync(received);
    }
}
