using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;

namespace Amanhecer.InMemory.Tests;

public class InMemoryGatewayProvisioningTests
{
    [Test]
    public async Task When_Publishing_Without_Starting_A_Consumer_Should_Provision_The_Queue()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            QueueName = $"amanhecer.{routingKey}",
            Provisioner = new CreateOrOverrideQueue()
        };

        var gateway = new InMemoryGateway { Publications = [publication] };

        // No ProvisionerAsync call: a publish-only application never starts the consumer
        // hosted service, so the producers have to provision the topology themselves.
        var producer = gateway.CreateProducers()[routingKey];

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        var queued = await gateway.Queues.GetChannels(publication.QueueName).Reader.ReadAsync();
        await Assert.That(queued).IsNotNull();
    }

    [Test]
    public async Task When_The_Publication_Has_No_Queue_Name_Should_Provision_The_Routing_Key()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            Provisioner = new CreateOrOverrideQueue()
        };

        var gateway = new InMemoryGateway { Publications = [publication] };

        await gateway.ProvisionerAsync();

        await Assert.That(gateway.Queues.Exists(routingKey)).IsTrue();

        var producer = gateway.CreateProducers()[routingKey];
        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        var queued = await gateway.Queues.GetChannels(routingKey).Reader.ReadAsync();
        await Assert.That(queued).IsNotNull();
    }

    [Test]
    public async Task When_Provisioning_Twice_Should_Keep_The_Queued_Messages()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            QueueName = routingKey,
            Provisioner = new CreateOrOverrideQueue()
        };

        var gateway = new InMemoryGateway { Publications = [publication] };

        await gateway.ProvisionerAsync();

        var message = CreateMessage();
        await gateway.CreateProducers()[routingKey].ProduceAsync(message, publication, new AmanhecerContext());

        // The consumer hosted service stops and starts again: re-provisioning must not replace
        // the channel, or the messages still queued in it are lost.
        await gateway.ProvisionerAsync();

        var queued = await gateway.Queues.GetChannels(routingKey).Reader.ReadAsync();
        await Assert.That(queued.Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_The_Queue_Was_Never_Provisioned_Should_Throw_A_Diagnosable_Error()
    {
        var queues = new QueueManagement();

        await Assert.That(() => queues.GetChannels("tests.missing"))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("tests.missing");
    }

    [Test]
    public async Task When_The_Queue_Is_Missing_Should_Record_The_Failure_On_The_Producer_Span()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var publication = new InMemoryPublication { RoutingKey = routingKey, QueueName = routingKey };
        var producer = new InMemoryProducer(new QueueManagement());

        using var parent = AmanhecerDiagnostics.ActivitySource.StartActivity("inmemory-test");

        System.Diagnostics.Activity? failed = null;
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhecerDiagnostics.ActivitySource.Name,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName == "Producer" && parent != null && activity.TraceId == parent.TraceId)
                {
                    failed = activity;
                }
            }
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        await Assert.That(async () => await producer.ProduceAsync(CreateMessage(),
                publication,
                new AmanhecerContext { Activity = parent }))
            .ThrowsExactly<InvalidOperationException>();

        // The span is stopped and carries the error: the lookup runs inside the instrumented block.
        await Assert.That(failed).IsNotNull();
        await Assert.That(failed!.Status).IsEqualTo(System.Diagnostics.ActivityStatusCode.Error);
    }

    [Test]
    public async Task When_Producing_In_Binary_Mode_Should_Set_The_CloudEvent_Headers()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var queues = new QueueManagement();
        queues.AddChannel(routingKey, Channel.CreateUnbounded<Message>());

        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            QueueName = routingKey,
            CloudEventType = CloudEventType.Binary,
            AdditionalCloudEvents = { ["tenant"] = "acme" }
        };

        var message = CreateMessage();
        await new InMemoryProducer(queues).ProduceAsync(message, publication, new AmanhecerContext());

        await Assert.That(message.Headers["cloudEvents:id"]).IsEqualTo(message.Id);
        await Assert.That(message.Headers["cloudEvents:type"]).IsEqualTo(message.Type);
        await Assert.That(message.Headers["cloudEvents:tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task When_Producing_In_Json_Mode_Should_Not_Set_The_CloudEvent_Headers()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var queues = new QueueManagement();
        queues.AddChannel(routingKey, Channel.CreateUnbounded<Message>());

        var publication = new InMemoryPublication
        {
            RoutingKey = routingKey,
            QueueName = routingKey,
            CloudEventType = CloudEventType.Json
        };

        var message = CreateMessage();
        await new InMemoryProducer(queues).ProduceAsync(message, publication, new AmanhecerContext());

        // In structured content mode the attributes live in the JSON envelope in the body.
        await Assert.That(message.Headers.ContainsKey("cloudEvents:id")).IsFalse();
    }

    [Test]
    public async Task When_Deferring_A_Message_Should_Requeue_It_After_The_Delay()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var queues = new QueueManagement();
        queues.AddChannel(routingKey, Channel.CreateUnbounded<Message>());

        var subscription = new InMemorySubscription(routingKey) { QueueName = routingKey };
        var consumer = new InMemoryConsumer(subscription, queues);

        var message = CreateMessage();
        await queues.GetChannels(routingKey).Writer.WriteAsync(message);

        using var cancellation = new CancellationTokenSource();
        await consumer.GetMessagesAsync(cancellation.Token);
        await consumer.DeferAsync(message, TimeSpan.FromMilliseconds(50));

        var requeued = await consumer.GetMessagesAsync(cancellation.Token);
        await Assert.That(requeued.Single().Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_Deferring_A_Message_After_Shutdown_Should_Not_Requeue_It()
    {
        var routingKey = $"tests.{Guid.NewGuid():N}";
        var queues = new QueueManagement();
        queues.AddChannel(routingKey, Channel.CreateUnbounded<Message>());

        var subscription = new InMemorySubscription(routingKey) { QueueName = routingKey };
        var consumer = new InMemoryConsumer(subscription, queues);

        var message = CreateMessage();
        await queues.GetChannels(routingKey).Writer.WriteAsync(message);

        using var cancellation = new CancellationTokenSource();
        await consumer.GetMessagesAsync(cancellation.Token);

        await consumer.DeferAsync(message, TimeSpan.FromMilliseconds(500));
        await cancellation.CancelAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        // The delayed requeue observes the pump token, so it stops with the host.
        await Assert.That(queues.GetChannels(routingKey).Reader.Count).IsEqualTo(0);
    }

    private static Message CreateMessage()
    {
        return new Message
        {
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message"
        };
    }
}
