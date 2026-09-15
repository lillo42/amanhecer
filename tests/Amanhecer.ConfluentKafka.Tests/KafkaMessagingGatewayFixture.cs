using System;
using System.Threading.Tasks;
using Amanhecer.ConfluentKafka;
using Amanhecer.ConfluentKafka.Provisioners;
using Amanhecer.Messaging.Base.Tests;
using Confluent.Kafka;
using Uuid = Amanhecer.Abstractions.Uuid;

namespace Amanhecer.ConfluentKafka.Tests;

/// <summary>
/// Builds <see cref="MessagingGatewayFixture"/>s backed by a Kafka cluster. Each fixture
/// gets its own topic and consumer group, so tests stay isolated and can run in parallel.
/// </summary>
internal static class KafkaMessagingGatewayFixture
{
    /// <summary>
    /// The bootstrap servers of the cluster the tests run against. Defaults to the broker
    /// started by the repository's docker-compose-kafka.yaml; override with the
    /// AMANHECER_KAFKA_BOOTSTRAP_SERVERS environment variable.
    /// </summary>
    public static readonly string BootstrapServers =
        Environment.GetEnvironmentVariable("AMANHECER_KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";

    /// <summary>
    /// Provisions an isolated topic on the cluster and returns a fixture bound to it.
    /// </summary>
    /// <param name="waitForConfirmation">Whether the fixture's publication waits for the
    /// broker's delivery confirmation when publishing.</param>
    /// <returns>The fixture used by the test; disposed once the test has run.</returns>
    public static async Task<MessagingGatewayFixture> CreateAsync(bool waitForConfirmation = false)
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var topic = $"amanhecer.tests.{suffix}";
        var groupId = $"amanhecer.tests.{suffix}";
        var routingKey = $"tests.{suffix}";

        var publication = new ConfluentKafkaPublication
        {
            RoutingKey = routingKey,
            Topic = topic,
            WaitForConfirmation = waitForConfirmation,
            Provisioner = new CreateTopic()
        };

        var subscription = new ConfluentKafkaSubscription(routingKey, topic, groupId)
        {
            Provisioner = new CreateTopic()
        };

        var gateway = new ConfluentKafkaGateway
        {
            BootstrapServers = BootstrapServers,
            Publications = [publication],
            Subscriptions = [subscription]
        };

        try
        {
            await gateway.ProvisionerAsync();

            return new MessagingGatewayFixture
            {
                Producer = gateway.CreateProducers()[publication.RoutingKey],
                Publication = publication,
                Consumer = gateway.CreateConsumer(subscription),
                Subscription = subscription,
                Cleanup = async () =>
                {
                    gateway.Dispose();
                    await DeleteTopicAsync(topic);
                }
            };
        }
        catch
        {
            gateway.Dispose();
            await DeleteTopicAsync(topic);
            throw;
        }
    }

    private static async Task DeleteTopicAsync(string topic)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
            await adminClient.DeleteTopicsAsync([topic]);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
