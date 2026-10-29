using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-backed tests for the super stream transport: producing through a
/// <see cref="RabbitMqSuperStreamPublication"/> and consuming from all partitions of the
/// super stream through a <see cref="RabbitMqSuperStreamSubscription"/>. Requires a broker
/// with super stream support (RabbitMQ 3.11+ with the <c>rabbitmq_stream</c> plugin).
/// </summary>
public class RabbitMqSuperStreamBrokerTests
{
    [Test]
    public async Task When_Producing_To_A_Super_Stream_Should_Receive_And_Ack_The_Message()
    {
        await using var fixture = await RabbitMqSuperStreamGatewayFixture.CreateAsync(partitions: 3);

        var producer = fixture.Gateway.CreateProducers()[fixture.RoutingKey];
        var consumer = fixture.Gateway.CreateConsumer(fixture.Subscription);

        var message = RabbitMqStreamMessagingGatewayFixture.CreateMessage();
        message.PartitionKey = "tests.partition";

        await producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await RabbitMqStreamMessagingGatewayFixture.ReceiveOneAsync(
            consumer, fixture.Subscription.ReceiveMessageTimeout);

        await Assert.That(received.PartitionKey).IsEqualTo("tests.partition");
        await consumer.AckAsync(received);
    }
}