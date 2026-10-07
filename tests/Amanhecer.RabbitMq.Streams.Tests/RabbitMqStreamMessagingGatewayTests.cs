using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Base.Tests;
using Amanhecer.RabbitMq.Streams.Provisioners;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-backed contract tests for <see cref="RabbitMqStreamGateway"/>: the shared messaging
/// gateway contract exercised against a real RabbitMQ broker with the stream plugin enabled.
/// Streams do not support redelivering deferred messages, so the defer-redelivery contract
/// test is skipped.
/// </summary>
[InheritsTests]
public class RabbitMqStreamMessagingGatewayTests : MessagingGatewayTests<RabbitMqStreamGateway>
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
        return new RabbitMqStreamPublication($"amanhecer.tests.{suffix}")
        {
            RoutingKey = routingKey ?? $"tests.{suffix}",
            Provisioner = CreateProvisioner(strategy)
        };
    }

    protected override ISubscription CreateSubscription(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var publication = Gateway.Publications.Cast<RabbitMqStreamPublication>().First();
        return new RabbitMqStreamSubscription(routingKey ?? publication.RoutingKey, publication.Stream)
        {
            BufferSize = 3,
            ReceiveMessageTimeout = TimeSpan.FromSeconds(30),
            Provisioner = CreateSubscriptionProvisioner(strategy)
        };
    }

    protected override async Task CleanupAsync()
    {
        await Gateway.DisposeAsync();

        var streams = Gateway.Publications.Select(x => x.Stream)
            .Concat(Gateway.Subscriptions.Select(x => x.Stream))
            .Distinct();

        foreach (var stream in streams)
        {
            await RabbitMqStreamMessagingGatewayFixture.DeleteStreamAsync(stream);
        }
    }

    // The streams provisioners implement both IPublicationProvisioner and ISubscriptionProvisioner,
    // so the same switch serves publications and subscriptions alike.
    private static IPublicationProvisioner CreateProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeStreamExists(),
            ProvisionerStrategy.Validate => new ValidateStreamExists(),
            _ => new CreateStream()
        };
    }

    private static ISubscriptionProvisioner CreateSubscriptionProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeStreamExists(),
            ProvisionerStrategy.Validate => new ValidateStreamExists(),
            _ => new CreateStream()
        };
    }
}
