using System;
using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory;
using Amanhecer.Messaging.Base.Tests;
using Amanhecer.InMemory.Provisioners;

namespace Amanhecer.InMemory.Tests;

[InheritsTests]
public class InMemoryMessagingGatewayTests : MessagingGatewayTests<InMemoryGateway>
{
    protected override TimeSpan MessagePropagateDelay => TimeSpan.Zero;

    protected override InMemoryGateway CreateGateway()
    {
        return new InMemoryGateway();
    }

    protected override IPublication CreatePublication(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var key = routingKey ?? $"tests.{suffix}";
        return new InMemoryPublication
        {
            RoutingKey = key,
            QueueName = $"amanhecer.tests.{suffix}",
            Provisioner = CreatePublicationProvisioner(strategy)
        };
    }

    protected override ISubscription CreateSubscription(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        var publication = (InMemoryPublication)Gateway.Publications.First();
        return new InMemorySubscription(routingKey ?? publication.RoutingKey, publication.QueueName)
        {
            Provisioner = CreateSubscriptionProvisioner(strategy)
        };
    }

    private static IPublicationProvisioner CreatePublicationProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new ValidateQueueExists(),
            ProvisionerStrategy.Validate => new ValidateQueueExists(),
            _ => new CreateOrOverrideQueue()
        };
    }

    private static ISubscriptionProvisioner CreateSubscriptionProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new ValidateQueueExists(),
            ProvisionerStrategy.Validate => new ValidateQueueExists(),
            _ => new CreateOrOverrideQueue()
        };
    }
}
