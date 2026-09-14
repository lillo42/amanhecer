using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;

namespace Amanhecer.InMemory.Tests;

public class InMemoryGatewayTests
{
    [Test]
    public async Task When_Creating_Producers_Should_Provision_The_Queues()
    {
        var publication = new InMemoryPublication
        {
            RoutingKey = $"tests.{Uuid.NewGuid():N}",
            Provisioner = new CreateOrOverrideQueue()
        };

        var gateway = new InMemoryGateway
        {
            Publications = [publication]
        };

        // No explicit ProvisionerAsync() call: the publish path provisions lazily.
        var producer = gateway.CreateProducers()[publication.RoutingKey];

        var message = new Message
        {
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message"
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = await gateway.Queues.GetChannel(publication.RoutingKey).Reader.ReadAsync(cts.Token);

        await Assert.That(received.Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_Two_Publications_Share_A_Routing_Key_Should_Throw()
    {
        var routingKey = $"tests.{Uuid.NewGuid():N}";
        var gateway = new InMemoryGateway
        {
            Publications =
            [
                new InMemoryPublication { RoutingKey = routingKey },
                new InMemoryPublication { RoutingKey = routingKey }
            ]
        };

        await Assert.That(() => gateway.CreateProducers())
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining(routingKey);
    }

    [Test]
    public async Task When_Publishing_To_A_Missing_Queue_With_AssumeExists_Should_Throw()
    {
        var publication = new InMemoryPublication
        {
            RoutingKey = $"tests.{Uuid.NewGuid():N}",
            Provisioner = new AssumeQueueExists()
        };

        var gateway = new InMemoryGateway
        {
            Publications = [publication]
        };

        var producer = gateway.CreateProducers()[publication.RoutingKey];
        var message = new Message
        {
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message"
        };

        await Assert.That(async () => await producer.ProduceAsync(message, publication, new AmanhecerContext()))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining(publication.RoutingKey);

        await Assert.That(async () => await producer.ProduceAsync(message, publication, new AmanhecerContext()))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("CreateOrOverride");
    }
}
