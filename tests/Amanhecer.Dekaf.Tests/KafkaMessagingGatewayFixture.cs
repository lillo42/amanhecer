using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Dekaf.Provisioners;
using Amanhecer.Messaging.Base.Tests;
using Dekaf;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Builds <see cref="MessagingTestFixture"/>s backed by a Kafka cluster. Each fixture
/// gets its own topic and consumer group, so tests stay isolated and can run in parallel.
/// </summary>
internal static class KafkaMessagingGatewayFixture
{
    /// <summary>
    /// The bootstrap servers of the cluster the tests run against. Dekaf's consumer requires
    /// the KIP-848 consumer group protocol (Kafka 4.0+), so the tests run against the Apache
    /// Kafka broker started by the repository's docker-compose-kafka.yaml; override with the
    /// AMANHECER_DEKAF_BOOTSTRAP_SERVERS environment variable.
    /// </summary>
    public static readonly string BootstrapServers =
        Environment.GetEnvironmentVariable("AMANHECER_DEKAF_BOOTSTRAP_SERVERS") ?? "localhost:9092";

    /// <summary>
    /// Provisions an isolated topic on the cluster and returns a fixture bound to it.
    /// </summary>
    /// <param name="waitForConfirmation">Whether the fixture's publication waits for the
    /// broker's delivery confirmation when publishing.</param>
    /// <returns>The fixture used by the test; disposed once the test has run.</returns>
    public static async Task<MessagingTestFixture> CreateAsync(bool waitForConfirmation = false)
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var topic = $"amanhecer.tests.{suffix}";
        var groupId = $"amanhecer.tests.{suffix}";
        var routingKey = $"tests.{suffix}";

        var publication = new DekafPublication
        {
            RoutingKey = routingKey,
            Topic = topic,
            WaitForConfirmation = waitForConfirmation,
            Provisioner = new CreateTopic()
        };

        var subscription = new DekafSubscription(routingKey, topic, groupId)
        {
            Provisioner = new CreateTopic()
        };

        var gateway = new DekafGateway
        {
            BootstrapServers = BootstrapServers,
            Publications = [publication],
            Subscriptions = [subscription]
        };

        try
        {
            await gateway.ProvisionerAsync();

            return new MessagingTestFixture
            {
                Producer = gateway.CreateProducers()[publication.RoutingKey],
                Publication = publication,
                Consumer = gateway.CreateConsumer(subscription),
                Subscription = subscription,
                Cleanup = async () =>
                {
                    await gateway.DisposeAsync();
                    await DeleteTopicAsync(topic);
                }
            };
        }
        catch
        {
            await gateway.DisposeAsync();
            await DeleteTopicAsync(topic);
            throw;
        }
    }

    public static async Task DeleteTopicAsync(string topic)
    {
        try
        {
            var adminClient = Kafka.CreateAdminClient()
                .WithBootstrapServers(BootstrapServers)
                .Build();
            await using (adminClient)
            {
                await adminClient.DeleteTopicsAsync([topic]);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
