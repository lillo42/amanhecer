using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Base.Tests;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-backed contract tests for <see cref="RabbitMqStreamGateway"/> against super streams:
/// the shared messaging gateway contract exercised with a <see cref="RabbitMqSuperStreamPublication"/>
/// and a <see cref="RabbitMqSuperStreamSubscription"/>. Requires a broker with super stream
/// support (RabbitMQ 3.11+ with the <c>rabbitmq_stream</c> plugin). Streams do not support
/// redelivering deferred messages, so the defer-redelivery contract test is skipped.
/// </summary>
[InheritsTests]
public class RabbitMqSuperStreamMessagingGatewayTests : MessagingGatewayTests<RabbitMqStreamGateway>
{
    protected override TimeSpan MessagePropagateDelay => TimeSpan.FromMilliseconds(500);

    protected override bool SupportsDeferRedeliveryTest => false;

    protected override RabbitMqStreamGateway CreateGateway()
    {
        return new RabbitMqStreamGateway
        {
            EndPoints = RabbitMqStreamMessagingGatewayFixture.CreateEndPoints()
        };
    }

    protected override IPublication CreatePublication(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new RabbitMqSuperStreamPublication(
            $"amanhecer.tests.super.{suffix}", CreateSuperStreamConfig())
        {
            RoutingKey = routingKey ?? $"tests.super.{suffix}",
            Provisioner = CreateProvisioner(strategy)
        };
    }

    protected override ISubscription CreateSubscription(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var publication = Gateway.Publications.Cast<RabbitMqSuperStreamPublication>().First();
        return new RabbitMqSuperStreamSubscription(routingKey ?? publication.RoutingKey, publication.Stream)
        {
            BufferSize = 3,
            ReceiveMessageTimeout = TimeSpan.FromSeconds(30),
            Provisioner = (ISubscriptionProvisioner)CreateProvisioner(strategy)
        };
    }

    protected override async Task CleanupAsync()
    {
        await Gateway.DisposeAsync();

        var superStreams = Gateway.Publications.Select(x => x.Stream)
            .Concat(Gateway.Subscriptions.Select(x => x.Stream))
            .Distinct();

        foreach (var superStream in superStreams)
        {
            await RabbitMqStreamMessagingGatewayFixture.DeleteSuperStreamAsync(superStream);
        }
    }

    private static SuperStreamConfig CreateSuperStreamConfig()
    {
        return new SuperStreamConfig
        {
            Routing = static message => message.Properties.GroupId ?? string.Empty
        };
    }

    // The super stream provisioners implement both IPublicationProvisioner and
    // ISubscriptionProvisioner, so the same switch serves publications and subscriptions alike.
    private static IPublicationProvisioner CreateProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeSuperStreamExists(),
            ProvisionerStrategy.Validate => new ValidateSuperStreamExists(),
            _ => new CreateSuperStream()
        };
    }
}
