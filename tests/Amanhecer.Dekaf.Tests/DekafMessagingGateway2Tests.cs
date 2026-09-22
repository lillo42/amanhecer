using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Dekaf.Provisioners;
using Amanhecer.Messaging.Base.Tests;

namespace Amanhecer.Dekaf.Tests;

[InheritsTests]
public class DekafMessagingGateway2Tests : MessagingGateway2Tests<DekafGateway>
{
    protected override TimeSpan MessagePropagateDelay => TimeSpan.FromSeconds(5);

    protected override DekafGateway CreateGateway()
    {
        return new DekafGateway
        {
            BootstrapServers = KafkaMessagingGatewayFixture.BootstrapServers
        };
    }

    protected override IPublication CreatePublication(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new DekafPublication
        {
            RoutingKey = routingKey ?? $"tests.{suffix}",
            Topic = $"amanhecer.tests.{suffix}",
            WaitForConfirmation = true,
            Provisioner = CreatePublicationProvisioner(strategy)
        };
    }

    protected override ISubscription CreateSubscription(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var publication = Gateway.Publications.First();
        return new DekafSubscription(
            routingKey ?? publication.RoutingKey,
            publication.Topic,
            $"amanhecer.tests.{Guid.NewGuid():N}")
        {
            BufferSize = 3,
            ReceiveMessageTimeout = TimeSpan.FromSeconds(30),
            Provisioner = CreateSubscriptionProvisioner(strategy)
        };
    }

    protected override async Task CleanupAsync()
    {
        await Gateway.DisposeAsync();
        foreach (var publication in Gateway.Publications)
        {
            await KafkaMessagingGatewayFixture.DeleteTopicAsync(publication.Topic);
        }
    }

    private static IPublicationProvisioner CreatePublicationProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeTopicExists(),
            ProvisionerStrategy.Validate => new ValidateTopicExists(),
            _ => new CreateTopic()
        };
    }

    private static ISubscriptionProvisioner CreateSubscriptionProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeTopicExists(),
            ProvisionerStrategy.Validate => new ValidateTopicExists(),
            _ => new CreateTopic()
        };
    }

    
    [RequiresUnreferencedCode("")]
    protected override async Task AssertMessageAsync(Message expected, Message received)
    {
        foreach (var pairValue in received.Headers.ToDictionary())
        {
            received.Headers[pairValue.Key] = Encoding.UTF8.GetString((byte[])pairValue.Value!);
        }
        
        await base.AssertMessageAsync(expected, received);
    }
}
