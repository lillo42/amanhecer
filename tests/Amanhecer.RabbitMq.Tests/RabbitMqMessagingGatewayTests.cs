using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Base.Tests;
using Amanhecer.RabbitMq.Provisioners;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Tests;

[InheritsTests]
public class RabbitMqMessagingGatewayTests : MessagingGatewayTests<RabbitMqGateway>
{
    protected override TimeSpan MessagePropagateDelay => TimeSpan.FromMilliseconds(250);

    protected override RabbitMqGateway CreateGateway()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"amanhecer.tests.{suffix}";

        return new RabbitMqGateway
        {
            AmqpUri = RabbitMqMessagingGatewayFixture.AmqpUri,
            Exchange = new Exchange
            {
                Name = exchangeName,
                Provisioner = new CreateIfNotExchange { Type = ExchangeType.Topic }
            }
        };
    }

    protected override IPublication CreatePublication(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        Gateway.Exchange!.Provisioner = CreateExchangeProvisioner(strategy);

        var key = routingKey ?? $"tests.{Guid.NewGuid():N}";

        return new RabbitMqPublication
        {
            RoutingKey = key,
            RabbitMqRoutingKey = key,
            Exchange = Gateway.Exchange
        };
    }

    protected override ISubscription CreateSubscription(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        string? routingKey = null)
    {
        Gateway.Exchange!.Provisioner = CreateExchangeProvisioner(strategy);

        var key = routingKey ?? $"tests.{Guid.NewGuid():N}";
        var queueName = $"amanhecer.tests.{Guid.NewGuid():N}";

        return new RabbitMqSubscription(key, queueName)
        {
            BufferSize = 3,
            Provisioner = CreateSubscriptionProvisioner(strategy, key)
        };
    }

    protected override async Task CleanupAsync()
    {
        foreach (var subscription in Gateway.Subscriptions)
        {
            await RabbitMqMessagingGatewayFixture.DeleteTopologyAsync(
                Gateway.Exchange!.Name,
                subscription.QueueName);
        }

        await Gateway.DisposeAsync();
    }

    private static IExchangeProvisioner CreateExchangeProvisioner(ProvisionerStrategy strategy)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeExchangeExists(),
            ProvisionerStrategy.Validate => new ValidateExchangeExists(),
            _ => new CreateIfNotExchange { Type = ExchangeType.Topic }
        };
    }

    private ISubscriptionProvisioner CreateSubscriptionProvisioner(ProvisionerStrategy strategy, string routingKey)
    {
        return strategy switch
        {
            ProvisionerStrategy.Assume => new AssumeQueueExists(),
            ProvisionerStrategy.Validate => new ValidateQueueExists { Exchange = Gateway.Exchange },
            _ => new CreateQueue
            {
                Exchange = Gateway.Exchange!,
                RoutingKey = routingKey,
                Durable = true
            }
        };
    }
}
