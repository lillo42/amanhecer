using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;

namespace Amanhecer.InMemory.Tests;

public class InMemoryProvisionersTests
{
    [Test]
    public async Task When_CreateOrOverride_Without_Capacity_Should_Create_An_Unbounded_Channel()
    {
        var gateway = new InMemoryGateway();
        var subscription = new InMemorySubscription("tests.routing", "tests.queue");

        await new CreateOrOverrideQueue().ExecuteAsync(gateway, subscription);

        var channel = gateway.Queues.GetChannel("tests.queue");
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(channel.Writer.TryWrite(new Message())).IsTrue();
        }
    }

    [Test]
    public async Task When_CreateOrOverride_With_Capacity_Should_Create_A_Bounded_Channel()
    {
        var gateway = new InMemoryGateway();
        var subscription = new InMemorySubscription("tests.routing", "tests.queue");

        await new CreateOrOverrideQueue { Capacity = 2 }.ExecuteAsync(gateway, subscription);

        var channel = gateway.Queues.GetChannel("tests.queue");
        await Assert.That(channel.Writer.TryWrite(new Message())).IsTrue();
        await Assert.That(channel.Writer.TryWrite(new Message())).IsTrue();
        await Assert.That(channel.Writer.TryWrite(new Message())).IsFalse();
    }

    [Test]
    public async Task When_Publication_Has_No_QueueName_Should_Fall_Back_To_The_Routing_Key()
    {
        var gateway = new InMemoryGateway();
        var publication = new InMemoryPublication
        {
            RoutingKey = "tests.routing"
        };

        await new CreateOrOverrideQueue().ExecuteAsync(gateway, publication);

        await Assert.That(gateway.Queues.Exists("tests.routing")).IsTrue();
    }

    [Test]
    public async Task When_Validate_Queue_Exists_And_The_Queue_Is_Missing_Should_Throw()
    {
        var gateway = new InMemoryGateway();
        var subscription = new InMemorySubscription("tests.routing", "tests.queue");

        await Assert.That(async () => await new ValidateQueueExists().ExecuteAsync(gateway, subscription))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("tests.queue");
    }

    [Test]
    public async Task When_Validate_Queue_Exists_And_The_Queue_Exists_Should_Pass()
    {
        var gateway = new InMemoryGateway();
        var subscription = new InMemorySubscription("tests.routing", "tests.queue");

        await new CreateOrOverrideQueue().ExecuteAsync(gateway, subscription);
        await new ValidateQueueExists().ExecuteAsync(gateway, subscription);
    }

    [Test]
    public async Task When_Validate_Publication_Queue_Exists_Should_Use_The_Routing_Key_Fallback()
    {
        var gateway = new InMemoryGateway();
        var publication = new InMemoryPublication
        {
            RoutingKey = "tests.routing"
        };

        await Assert.That(async () => await new ValidateQueueExists().ExecuteAsync(gateway, publication))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("tests.routing");
    }

    [Test]
    public async Task When_Assume_Queue_Exists_Should_Do_Nothing()
    {
        var gateway = new InMemoryGateway();
        var subscription = new InMemorySubscription("tests.routing", "tests.queue");
        var publication = new InMemoryPublication
        {
            RoutingKey = "tests.routing"
        };

        var provisioner = new AssumeQueueExists();
        await provisioner.ExecuteAsync(gateway, subscription);
        await provisioner.ExecuteAsync(gateway, publication);

        await Assert.That(gateway.Queues.Exists("tests.queue")).IsFalse();
        await Assert.That(gateway.Queues.Exists("tests.routing")).IsFalse();
    }
}
