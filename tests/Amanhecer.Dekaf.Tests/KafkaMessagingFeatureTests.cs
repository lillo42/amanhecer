using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Dekaf;
using Amanhecer.Dekaf.Provisioners;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Broker-backed feature tests for the Dekaf transport: delivery confirmation, provisioner
/// behavior against the cluster and header/partition-key round trips. Requires a Kafka 4.0+
/// broker (see docker-compose-kafka.yaml at the repository root).
/// </summary>
public class KafkaMessagingFeatureTests
{
    private static readonly TimeSpan s_receiveTimeout = TimeSpan.FromSeconds(30);

    private static Message CreateMessage()
    {
        return new Message
        {
            ContentType = new System.Net.Mime.ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = Encoding.UTF8.GetBytes(Uuid.NewGuid().ToString())
        };
    }

    private static async Task<Message> ReceiveOneAsync(Messaging.Base.Tests.MessagingGatewayFixture fixture)
    {
        using var cts = new CancellationTokenSource(s_receiveTimeout);
        var messages = await fixture.Consumer.GetMessagesAsync(cts.Token);
        return messages[0];
    }

    [Test]
    public async Task When_WaitForConfirmation_Should_Publish_And_Be_Received()
    {
        await using var fixture = await KafkaMessagingGatewayFixture.CreateAsync(waitForConfirmation: true);
        var message = CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture);

        await Assert.That(received.Id).IsEqualTo(message.Id);
        await fixture.Consumer.AckAsync(received);
    }

    [Test]
    public async Task ValidateTopicExists_When_Topic_Is_Missing_Should_Throw()
    {
        var topic = $"amanhecer.tests.missing.{Uuid.NewGuid():N}";
        var gateway = new DekafGateway
        {
            BootstrapServers = KafkaMessagingGatewayFixture.BootstrapServers
        };

        try
        {
            var provisioner = new ValidateTopicExists();
            var publication = new DekafPublication { RoutingKey = "tests", Topic = topic };

            await Assert.That(async () => await provisioner.ExecuteAsync(gateway, publication))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            await gateway.DisposeAsync();
        }
    }

    [Test]
    public async Task CreateTopic_When_Topic_Already_Exists_Should_Not_Throw()
    {
        var topic = $"amanhecer.tests.{Uuid.NewGuid():N}";
        var gateway = new DekafGateway
        {
            BootstrapServers = KafkaMessagingGatewayFixture.BootstrapServers
        };

        try
        {
            var provisioner = new CreateTopic();
            var publication = new DekafPublication { RoutingKey = "tests", Topic = topic };

            await provisioner.ExecuteAsync(gateway, publication);
            await provisioner.ExecuteAsync(gateway, publication);
        }
        finally
        {
            await gateway.DisposeAsync();
        }
    }

    [Test]
    public async Task When_Producing_With_PartitionKey_Should_Round_Trip()
    {
        await using var fixture = await KafkaMessagingGatewayFixture.CreateAsync();
        var message = CreateMessage();
        message.PartitionKey = "partition-key";

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture);

        await Assert.That(received.PartitionKey).IsEqualTo("partition-key");
        await fixture.Consumer.AckAsync(received);
    }

    [Test]
    public async Task When_Producing_With_NonAscii_Header_Should_Round_Trip_As_Utf8()
    {
        await using var fixture = await KafkaMessagingGatewayFixture.CreateAsync();
        var message = CreateMessage();
        message.Headers["greeting"] = "héllo wörld ✨";

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture);

        await Assert.That(received.Headers["greeting"]).IsTypeOf<byte[]>();
        await Assert.That(Encoding.UTF8.GetString((byte[])received.Headers["greeting"]!))
            .IsEqualTo("héllo wörld ✨");
        await fixture.Consumer.AckAsync(received);
    }
}
