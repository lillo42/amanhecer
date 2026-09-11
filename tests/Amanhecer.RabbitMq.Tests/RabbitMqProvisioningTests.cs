using System.Threading.Tasks;
using Amanhecer.RabbitMq.Provisioners;
using RabbitMQ.Client.Exceptions;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Tests for the exchange and queue provisioners run against a real broker: validating,
/// creating and assuming the topology a gateway needs.
/// Requires a broker (see docker-compose-rabbitmq.yaml at the repository root).
/// </summary>
public class RabbitMqProvisioningTests
{
    [Test]
    public async Task When_Validating_A_Missing_Exchange_Should_Throw()
    {
        await Assert.That(async () => await RabbitMqMessagingGatewayFixture.CreateAsync(
                exchangeProvisioner: new ValidateExchangeExists()))
            .Throws<OperationInterruptedException>();
    }

    [Test]
    public async Task When_Validating_A_Missing_Queue_Should_Throw()
    {
        await Assert.That(async () => await RabbitMqMessagingGatewayFixture.CreateAsync(
                subscriptionProvisioner: new ValidateQueueExists()))
            .Throws<OperationInterruptedException>();
    }

    [Test]
    public async Task When_Validating_Existing_Topology_Should_Succeed()
    {
        await using var fixture = await RabbitMqMessagingGatewayFixture.CreateAsync();
        var publication = (RabbitMqPublication)fixture.Publication;
        var subscription = (RabbitMqSubscription)fixture.Subscription;

        var exchange = new Exchange
        {
            Name = publication.Exchange!.Name,
            Provisioner = new ValidateExchangeExists()
        };

        await using var gateway = new RabbitMqGateway
        {
            AmqpUri = RabbitMqMessagingGatewayFixture.AmqpUri,
            Exchange = exchange,
            Publications =
            [
                new RabbitMqPublication
                {
                    RoutingKey = publication.RoutingKey,
                    RabbitMqRoutingKey = publication.RabbitMqRoutingKey,
                    Exchange = exchange
                }
            ],
            Subscriptions =
            [
                new RabbitMqSubscription(subscription.ToRoutingKey, subscription.QueueName)
                {
                    Provisioner = new ValidateQueueExists { Exchange = exchange }
                }
            ]
        };

        await gateway.ProvisionerAsync();

        await Assert.That(gateway.CreateProducers().ContainsKey(publication.RoutingKey)).IsTrue();
    }

    [Test]
    public async Task When_Assuming_Missing_Topology_Should_Not_Provision()
    {
        // Provisioning is a no-op, so nothing is declared on the broker; consuming from the
        // undeclared queue then fails when the broker closes the channel with a 404.
        await Assert.That(async () => await RabbitMqMessagingGatewayFixture.CreateAsync(
                exchangeProvisioner: new AssumeExchangeExists(),
                subscriptionProvisioner: new AssumeQueueExists()))
            .Throws<OperationInterruptedException>();
    }
}
