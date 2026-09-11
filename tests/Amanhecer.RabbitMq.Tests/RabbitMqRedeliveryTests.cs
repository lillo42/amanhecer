using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Tests for redelivery: deferring a message requeues it, and the redelivered message is
/// stamped with the <see cref="MetadataName.Redelivered"/> metadata.
/// Requires a broker (see docker-compose-rabbitmq.yaml at the repository root).
/// </summary>
public class RabbitMqRedeliveryTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task When_Deferring_A_Message_Should_Be_Marked_As_Redelivered()
    {
        await using var fixture = await RabbitMqMessagingGatewayFixture.CreateAsync();
        var message = RabbitMqMessagingGatewayFixture.CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await RabbitMqMessagingGatewayFixture.ReceiveOneAsync(fixture, ReceiveTimeout);
        await Assert.That((bool?)received.Metadata[MetadataName.Redelivered]).IsFalse();

        await fixture.Consumer.DeferAsync(received, TimeSpan.FromMilliseconds(50));

        var redelivered = await RabbitMqMessagingGatewayFixture.ReceiveOneAsync(fixture, ReceiveTimeout);

        await Assert.That(redelivered.Id).IsEqualTo(message.Id);
        await Assert.That((bool?)redelivered.Metadata[MetadataName.Redelivered]).IsTrue();

        await fixture.Consumer.AckAsync(redelivered);
    }
}
