using System;
using System.Threading.Tasks;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Gateway fixture for the broker-backed super stream tests: provisions an isolated super
/// stream with the given number of partitions, wires a <see cref="RabbitMqSuperStreamPublication"/>
/// and a <see cref="RabbitMqSuperStreamSubscription"/> to it, and cleans the super stream up
/// on disposal. Each test gets its own super stream, so tests stay isolated and can run in
/// parallel.
/// </summary>
internal sealed class RabbitMqSuperStreamGatewayFixture : IAsyncDisposable
{
    private RabbitMqSuperStreamGatewayFixture(
        RabbitMqStreamGateway gateway,
        RabbitMqSuperStreamPublication publication,
        RabbitMqSuperStreamSubscription subscription,
        string superStream)
    {
        Gateway = gateway;
        Publication = publication;
        Subscription = subscription;
        SuperStream = superStream;
    }

    /// <summary>Gets the provisioned gateway; producers and consumers are created from it.</summary>
    public RabbitMqStreamGateway Gateway { get; }

    /// <summary>Gets the publication messages are produced through.</summary>
    public RabbitMqSuperStreamPublication Publication { get; }

    /// <summary>Gets the subscription messages are consumed from.</summary>
    public RabbitMqSuperStreamSubscription Subscription { get; }

    /// <summary>Gets the name of the super stream backing this fixture.</summary>
    public string SuperStream { get; }

    /// <summary>Gets the routing key resolving the publication.</summary>
    public string RoutingKey => Publication.RoutingKey;

    /// <summary>
    /// Creates a gateway wired to a fresh super stream with the given number of partitions.
    /// Messages are routed to a partition by hashing the AMQP <c>GroupId</c> property
    /// (mapped from <c>Message.PartitionKey</c>). The super stream is provisioned before
    /// the fixture is returned.
    /// </summary>
    /// <param name="partitions">The number of partitions of the super stream.</param>
    /// <returns>The provisioned fixture; dispose it to close the gateway and delete the
    /// super stream.</returns>
    public static async Task<RabbitMqSuperStreamGatewayFixture> CreateAsync(int partitions = 3)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var superStream = $"amanhecer.tests.super.{suffix}";
        var routingKey = $"tests.super.{suffix}";

        var publication = new RabbitMqSuperStreamPublication(superStream, new SuperStreamConfig
        {
            Routing = static message => message.Properties.GroupId ?? string.Empty
        })
        {
            RoutingKey = routingKey,
            Provisioner = new CreateSuperStream { StreamType = new PartitionSuperStream(partitions) }
        };

        var subscription = new RabbitMqSuperStreamSubscription(routingKey, superStream)
        {
            ReceiveMessageTimeout = TimeSpan.FromSeconds(30),
            Provisioner = new CreateSuperStream { StreamType = new PartitionSuperStream(partitions) }
        };

        var gateway = new RabbitMqStreamGateway
        {
            EndPoints = RabbitMqStreamMessagingGatewayFixture.CreateEndPoints(),
            Publications = [publication],
            Subscriptions = [subscription]
        };

        await gateway.ProvisionerAsync();

        return new RabbitMqSuperStreamGatewayFixture(gateway, publication, subscription, superStream);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Gateway.DisposeAsync();
        await RabbitMqStreamMessagingGatewayFixture.DeleteSuperStreamAsync(SuperStream);
    }
}
