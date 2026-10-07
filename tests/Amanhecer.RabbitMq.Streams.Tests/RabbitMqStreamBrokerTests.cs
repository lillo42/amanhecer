using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-backed tests for the RabbitMQ Streams transport: offset tracking on ack and the
/// expiration round-trip.
/// </summary>
public class RabbitMqStreamBrokerTests
{
    [Test]
    public async Task When_Acking_A_Message_Should_Store_The_Offset_On_The_Broker()
    {
        var (gateway, publication, subscription) = CreateGateway(out var stream, out var routingKey);

        try
        {
            await gateway.ProvisionerAsync();
            var producer = gateway.CreateProducers()[routingKey];
            var consumer = gateway.CreateConsumer(subscription);

            var message = RabbitMqStreamMessagingGatewayFixture.CreateMessage();
            await producer.ProduceAsync(message, publication, new AmanhecerContext());

            var received = await RabbitMqStreamMessagingGatewayFixture.ReceiveOneAsync(
                consumer, subscription.ReceiveMessageTimeout);
            await consumer.AckAsync(received);

            // StoreOffset is fire-and-forget in the stream protocol, so poll until the
            // broker has applied it.
            var system = await RabbitMqStreamMessagingGatewayFixture.CreateStreamSystemAsync();
            try
            {
                ulong? offset = null;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (offset == null && !cts.IsCancellationRequested)
                {
                    offset = await system.TryQueryOffset(subscription.Name, stream);
                    if (offset == null)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token)
                            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    }
                }

                await Assert.That(offset).IsEqualTo(0ul);
            }
            finally
            {
                await system.Close();
            }
        }
        finally
        {
            await gateway.DisposeAsync();
            await RabbitMqStreamMessagingGatewayFixture.DeleteStreamAsync(stream);
        }
    }

    [Test]
    public async Task When_The_Message_Metadata_Sets_An_Expiration_Should_Round_Trip()
    {
        var (gateway, publication, subscription) = CreateGateway(out var stream, out var routingKey);

        try
        {
            await gateway.ProvisionerAsync();
            var producer = gateway.CreateProducers()[routingKey];
            var consumer = gateway.CreateConsumer(subscription);

            var message = RabbitMqStreamMessagingGatewayFixture.CreateMessage();
            var expireAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            message.Metadata[Metadata.Expiration] = expireAt;

            await producer.ProduceAsync(message, publication, new AmanhecerContext());

            var received = await RabbitMqStreamMessagingGatewayFixture.ReceiveOneAsync(
                consumer, subscription.ReceiveMessageTimeout);

            await Assert.That(received.Metadata[Metadata.Expiration]).IsEqualTo(expireAt);
        }
        finally
        {
            await gateway.DisposeAsync();
            await RabbitMqStreamMessagingGatewayFixture.DeleteStreamAsync(stream);
        }
    }

    [Test]
    public async Task When_The_Context_Sets_An_Expiration_Should_Map_The_Absolute_Expiry_Time()
    {
        var (gateway, publication, subscription) = CreateGateway(out var stream, out var routingKey);

        try
        {
            await gateway.ProvisionerAsync();
            var producer = gateway.CreateProducers()[routingKey];
            var consumer = gateway.CreateConsumer(subscription);

            var context = new AmanhecerContext();
            context.Metadata[Metadata.Expiration] = TimeSpan.FromHours(1);

            var before = DateTime.UtcNow;
            await producer.ProduceAsync(RabbitMqStreamMessagingGatewayFixture.CreateMessage(),
                publication, context);

            var received = await RabbitMqStreamMessagingGatewayFixture.ReceiveOneAsync(
                consumer, subscription.ReceiveMessageTimeout);

            await Assert.That(received.Metadata[Metadata.Expiration]).IsTypeOf<DateTime>();
            var expireAt = (DateTime)received.Metadata[Metadata.Expiration]!;
            await Assert.That(expireAt).IsBetween(before.AddMinutes(55), before.AddMinutes(65));
        }
        finally
        {
            await gateway.DisposeAsync();
            await RabbitMqStreamMessagingGatewayFixture.DeleteStreamAsync(stream);
        }
    }

    private static (RabbitMqStreamGateway Gateway,
        RabbitMqStreamPublication Publication,
        RabbitMqStreamSubscription Subscription) CreateGateway(out string stream, out string routingKey)
    {
        var suffix = Guid.NewGuid().ToString("N");
        stream = $"amanhecer.tests.{suffix}";
        routingKey = $"tests.{suffix}";

        var publication = new RabbitMqStreamPublication(stream)
        {
            RoutingKey = routingKey,
            Provisioner = new CreateStream()
        };

        var subscription = new RabbitMqStreamSubscription(routingKey, stream)
        {
            ReceiveMessageTimeout = TimeSpan.FromSeconds(30),
            Provisioner = new CreateStream()
        };

        var gateway = new RabbitMqStreamGateway
        {
            EndPoints = RabbitMqStreamMessagingGatewayFixture.CreateEndPoints(),
            Publications = [publication],
            Subscriptions = [subscription]
        };

        return (gateway, publication, subscription);
    }
}
