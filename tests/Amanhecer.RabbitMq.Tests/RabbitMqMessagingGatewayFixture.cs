using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
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

    /// <summary>
    /// Provisions an isolated exchange/queue pair on the broker and returns a fixture bound
    /// to it.
    /// </summary>
    /// <param name="configureQueue">
    /// Optional delegate that tweaks the <see cref="CreateQueue"/> subscription provisioner
    /// (queue arguments, durability, binding routing key, ...) before the queue is declared.
    /// Ignored when <paramref name="subscriptionProvisioner"/> is set.
    /// </param>
    /// <param name="exchangeProvisioner">
    /// Optional provisioner used to set the exchange up; defaults to
    /// <see cref="CreateIfNotExchange"/>.
    /// </param>
    /// <param name="subscriptionProvisioner">
    /// Optional provisioner used to set the queue up; defaults to a durable
    /// <see cref="CreateQueue"/>.
    /// </param>
    /// <param name="publicationRoutingKey">
    /// Optional routing key the fixture's publication publishes with; defaults to the routing
    /// key the queue is bound with.
    /// </param>
    /// <param name="provisionDeadLetterQueue">
    /// When <see langword="true"/>, also provisions a dead-letter exchange and queue pair
    /// (named after the fixture's exchange and queue with a <c>.dlx</c>/<c>.dlq</c> suffix)
    /// and points the fixture's queue at them through the <c>x-dead-letter-exchange</c> and
    /// <c>x-dead-letter-routing-key</c> queue arguments.
    /// </param>
    /// <returns>The fixture used by the test; disposed once the test has run.</returns>
    public static async Task<MessagingGatewayFixture> CreateAsync(
        Action<CreateQueue>? configureQueue = null,
        IExchangeProvisioner? exchangeProvisioner = null,
        ISubscriptionProvisioner? subscriptionProvisioner = null,
        string? publicationRoutingKey = null,
        bool provisionDeadLetterQueue = false)
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var exchangeName = $"amanhecer.tests.{suffix}";
        var queueName = $"amanhecer.tests.{suffix}";
        var routingKey = $"tests.{suffix}";

        var exchange = new Exchange
        {
            Name = exchangeName,
            Provisioner = exchangeProvisioner ?? new CreateIfNotExchange { Type = ExchangeType.Topic }
        };

        var publication = new RabbitMqPublication
        {
            RoutingKey = publicationRoutingKey ?? routingKey,
            RabbitMqRoutingKey = publicationRoutingKey ?? routingKey,
            Exchange = exchange
        };

        // Durable: RabbitMQ 4.x rejects transient non-exclusive queues by default.
        var queueProvisioner = new CreateQueue { Exchange = exchange, RoutingKey = routingKey, Durable = true };

        string? deadLetterExchangeName = null;
        string? deadLetterQueueName = null;
        string? deadLetterRoutingKey = null;
        if (provisionDeadLetterQueue)
        {
            deadLetterExchangeName = $"{exchangeName}.dlx";
            deadLetterQueueName = $"{queueName}.dlq";
            deadLetterRoutingKey = $"{routingKey}.dead";

            queueProvisioner.QueueArguments["x-dead-letter-exchange"] = deadLetterExchangeName;
            queueProvisioner.QueueArguments["x-dead-letter-routing-key"] = deadLetterRoutingKey;
        }

        configureQueue?.Invoke(queueProvisioner);

        var subscription = new RabbitMqSubscription(routingKey, queueName)
        {
            Provisioner = subscriptionProvisioner ?? queueProvisioner
        };

        var gateway = new RabbitMqGateway
        {
            AmqpUri = AmqpUri,
            Exchange = exchange,
            Publications = [publication],
            Subscriptions = [subscription]
        };

        try
        {
            if (deadLetterExchangeName != null)
            {
                await ProvisionDeadLetterTopologyAsync(
                    deadLetterExchangeName, deadLetterQueueName!, deadLetterRoutingKey!);
            }

            await gateway.ProvisionerAsync();

            return new MessagingGatewayFixture
            {
                Producer = gateway.CreateProducers()[publication.RoutingKey],
                Publication = publication,
                Consumer = gateway.CreateConsumer(subscription),
                Subscription = subscription,
                Cleanup = async () =>
                {
                    await gateway.DisposeAsync();
                    await DeleteTopologyAsync(
                        exchangeName, queueName, deadLetterExchangeName, deadLetterQueueName);
                }
            };
        }
        catch
        {
            await DeleteTopologyAsync(exchangeName, queueName, deadLetterExchangeName, deadLetterQueueName);
            await gateway.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates the message produced by the tests: a message with a unique id and payload.
    /// </summary>
    /// <returns>A new message.</returns>
    public static Message CreateMessage()
    {
        return new Message
        {
            ContentType = new ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = Encoding.UTF8.GetBytes(Uuid.NewGuid().ToString())
        };
    }

    /// <summary>
    /// Receives the next message from the fixture's consumer, failing after
    /// <paramref name="timeout"/> when none arrives.
    /// </summary>
    /// <param name="fixture">The fixture whose consumer receives the message.</param>
    /// <param name="timeout">How long to wait for the message.</param>
    /// <returns>The received message.</returns>
    public static async Task<Message> ReceiveOneAsync(MessagingGatewayFixture fixture, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var messages = await fixture.Consumer.GetMessagesAsync(cts.Token);
        return messages[0];
    }

    /// <summary>
    /// Reads a single message straight off <paramref name="queueName"/> through a raw channel,
    /// polling <c>BasicGet</c> until a message arrives or <paramref name="timeout"/> elapses.
    /// Used to assert the contents of queues no gateway consumer reads from, such as
    /// dead-letter queues.
    /// </summary>
    /// <param name="queueName">The queue to read from.</param>
    /// <param name="timeout">How long to wait for a message.</param>
    /// <returns>The message read from the queue, or <see langword="null"/> on timeout.</returns>
    public static async Task<Message?> GetOneRawAsync(string queueName, TimeSpan timeout)
    {
        var factory = new ConnectionFactory { Uri = AmqpUri };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true);
            if (result != null)
            {
                return new Message
                {
                    Id = result.BasicProperties.MessageId ?? Uuid.NewGuid().ToString(),
                    Headers = result.BasicProperties.Headers == null
                        ? []
                        : new Dictionary<string, object?>(result.BasicProperties.Headers),
                    Payload = result.Body.ToArray()
                };
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return null;
    }

    /// <summary>
    /// Deletes the exchange and queue (and optionally the dead-letter exchange and queue) a
    /// fixture provisioned, tolerating resources that are already gone.
    /// </summary>
    /// <param name="exchangeName">The exchange to delete.</param>
    /// <param name="queueName">The queue to delete.</param>
    /// <param name="deadLetterExchangeName">The dead-letter exchange to delete, if any.</param>
    /// <param name="deadLetterQueueName">The dead-letter queue to delete, if any.</param>
    public static async Task DeleteTopologyAsync(string exchangeName,
        string queueName,
        string? deadLetterExchangeName = null,
        string? deadLetterQueueName = null)
    {
        var factory = new ConnectionFactory { Uri = AmqpUri };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await TryDeleteQueueAsync(channel, queueName);
        if (deadLetterQueueName != null)
        {
            await TryDeleteQueueAsync(channel, deadLetterQueueName);
        }

        await TryDeleteExchangeAsync(channel, exchangeName);
        if (deadLetterExchangeName != null)
        {
            await TryDeleteExchangeAsync(channel, deadLetterExchangeName);
        }
    }

    private static async Task ProvisionDeadLetterTopologyAsync(string exchangeName,
        string queueName,
        string routingKey)
    {
        var factory = new ConnectionFactory { Uri = AmqpUri };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync(queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey);
    }

    private static async Task TryDeleteQueueAsync(IChannel channel, string queueName)
    {
        try
        {
            await channel.QueueDeleteAsync(queueName);
        }
        catch (OperationInterruptedException)
        {
            // The queue is already gone (for example auto-deleted): nothing to clean up.
        }
    }

    private static async Task TryDeleteExchangeAsync(IChannel channel, string exchangeName)
    {
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
