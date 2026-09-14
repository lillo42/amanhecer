using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Tests;

public class InMemoryProducerTelemetryTests
{
    [Test]
    public async Task When_Producing_Should_Tag_Producer_Activity_With_Messaging_System()
    {
        var queueName = $"tests.{Guid.NewGuid():N}";
        var queues = new QueueManagement();
        queues.AddChannel(queueName, Channel.CreateUnbounded<Message>());

        var producer = new InMemoryProducer(queues);
        var publication = new InMemoryPublication
        {
            RoutingKey = queueName,
            QueueName = queueName
        };

        var message = new Message
        {
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message"
        };

        using var parent = AmanhecerDiagnostics.ActivitySource.StartActivity("inmemory-test", ActivityKind.Internal);

        Activity? published = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhecerDiagnostics.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName == "Producer" && parent != null && activity.TraceId == parent.TraceId)
                {
                    published = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        await producer.ProduceAsync(message, publication, new AmanhecerContext { Activity = parent });

        await Assert.That(published).IsNotNull();
        await Assert.That(published!.GetTagItem("messaging.system")).IsEqualTo("inmemory");
        await Assert.That(published.GetTagItem("messaging.operation.type")).IsEqualTo("publish");
        await Assert.That(published.GetTagItem("messaging.destination.name")).IsEqualTo(queueName);
    }
}
