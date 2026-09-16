using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.InMemory.Provisioners;
using Amanhecer.Messaging.Base.Tests;

namespace Amanhecer.InMemory.Tests;

internal static class InMemoryMessagingGatewayFixture
{
    public static Task<MessagingGatewayFixture> CreateAsync()
    {
        var suffix = Uuid.NewGuid().ToString("N");
        var routingKey = $"tests.{suffix}";
        var queueName = $"amanhecer.tests.{suffix}";

        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            QueueName = queueName,
            Provisioner = new CreateOrOverrideQueue()
        };

        var subscription = new InMemorySubscription(routingKey, queueName)
        {
            Provisioner = new ValidateQueueExists()
        };

        var gateway = new InMemoryGateway
        {
            Publications = [publication],
            Subscriptions = [subscription]
        };

        gateway.ProvisionerAsync().GetAwaiter().GetResult();

        return Task.FromResult(new MessagingGatewayFixture
        {
            Producer = gateway.CreateProducers()[publication.RoutingKey],
            Publication = publication,
            Consumer = gateway.CreateConsumer(subscription),
            Subscription = subscription,
            Cleanup = () => ValueTask.CompletedTask
        });
    }
}
