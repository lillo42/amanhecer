using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Messaging.Base.Tests;
using Amanhecer.RabbitMq.Provisioners;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Builds <see cref="MessagingGatewayFixture"/>s backed by a RabbitMQ broker. Each fixture
/// gets its own exchange and queue, so tests stay isolated and can run in parallel.
/// </summary>
internal static class RabbitMqMessagingGatewayFixture
{
    /// <summary>
    /// The AMQP URI of the broker the tests run against. Defaults to the broker started by
    /// the repository's docker-compose-rabbitmq.yaml; override with the
    /// AMANHECER_RABBITMQ_URI environment variable.
    /// </summary>
    public static readonly Uri AmqpUri = new(
        Environment.GetEnvironmentVariable("AMANHECER_RABBITMQ_URI") ?? "amqp://guest:guest@localhost:5672");

    public static async Task<MessagingGatewayFixture> CreateAsync()
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var exchangeName = $"amanhecer.tests.{suffix}";
        var queueName = $"amanhecer.tests.{suffix}";
        var routingKey = $"tests.{suffix}";

        var exchange = new Exchange
        {
            Name = exchangeName,
            Provisioner = new CreateIfNotExchange { Type = ExchangeType.Topic }
        };

        var publication = new RabbitMqPublication
        {
            RoutingKey = routingKey,
            RabbitMqRoutingKey = routingKey,
            Exchange = exchange
        };

        var subscription = new RabbitMqSubscription(routingKey, queueName)
        {
            // Durable: RabbitMQ 4.x rejects transient non-exclusive queues by default.
            Provisioner = new CreateQueue { Exchange = exchange, RoutingKey = routingKey, Durable = true }
        };

        var gateway = new RabbitMqGateway
        {
            AmqpUri = AmqpUri,
            Exchange = exchange,
            Publications = [publication],
            Subscriptions = [subscription]
        };

        await gateway.ProvisionerAsync();

        return new MessagingGatewayFixture
        {
            Producer = gateway.CreateProducers()[routingKey],
            Publication = publication,
            Consumer = gateway.CreateConsumer(subscription),
            Subscription = subscription,
            Cleanup = async () =>
            {
                await gateway.DisposeAsync();
                await DeleteTopologyAsync(exchangeName, queueName);
            }
        };
    }

    private static async Task DeleteTopologyAsync(string exchangeName, string queueName)
    {
        var factory = new ConnectionFactory { Uri = AmqpUri };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.QueueDeleteAsync(queueName);
        }
        catch (OperationInterruptedException)
        {
            // The queue is already gone (for example auto-deleted): nothing to clean up.
        }

        try
        {
            await channel.ExchangeDeleteAsync(exchangeName);
        }
        catch (OperationInterruptedException)
        {
            // The exchange is already gone: nothing to clean up.
        }
    }
}
