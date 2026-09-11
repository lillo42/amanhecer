using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Tests for broker-level dead-lettering: a queue declared with dead-letter arguments moves
/// messages that are nacked without requeue to its dead-letter exchange.
/// Requires a broker (see docker-compose-rabbitmq.yaml at the repository root).
/// </summary>
public class RabbitMqDeadLetterTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task When_Nacking_A_Message_Should_Be_Dead_Lettered()
    {
        await using var fixture = await RabbitMqMessagingGatewayFixture.CreateAsync(
            provisionDeadLetterQueue: true);
        var message = RabbitMqMessagingGatewayFixture.CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await RabbitMqMessagingGatewayFixture.ReceiveOneAsync(fixture, ReceiveTimeout);
        await fixture.Consumer.NackAsync(received);

        var queueName = ((RabbitMqSubscription)fixture.Subscription).QueueName;
        var deadLettered = await RabbitMqMessagingGatewayFixture.GetOneRawAsync(
            $"{queueName}.dlq", ReceiveTimeout);

        await Assert.That(deadLettered).IsNotNull();
        await Assert.That(deadLettered!.Payload.Span.SequenceEqual(message.Payload.Span)).IsTrue();
    }
}
