using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;

namespace Amanhecer.InMemory.Tests;

public class InMemoryGatewayTests
{
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
