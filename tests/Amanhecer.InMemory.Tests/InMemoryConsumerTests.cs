using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Tests;

public class InMemoryConsumerTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private static (InMemoryConsumer Consumer, Channel<Message> Channel) CreateConsumer(
        Action<InMemorySubscription>? configure = null)
    {
        var queueName = $"tests.{Guid.NewGuid():N}";
        var channel = Channel.CreateUnbounded<Message>();
        var queues = new QueueManagement();
        queues.AddChannel(queueName, channel);

        var subscription = new InMemorySubscription("tests.routing", queueName);
        configure?.Invoke(subscription);

        return (new InMemoryConsumer(subscription, queues), channel);
    }

    [Test]
    public async Task When_Deferring_With_Zero_Delay_Should_Reenqueue_The_Message()
    {
        var (consumer, channel) = CreateConsumer();
        var message = new Message();

        await channel.Writer.WriteAsync(message);
        using var cts = new CancellationTokenSource(ReceiveTimeout);

        var received = await consumer.GetMessagesAsync(cts.Token);
        await consumer.DeferAsync(received[0], TimeSpan.Zero);

        var requeued = await consumer.GetMessagesAsync(cts.Token);

        await Assert.That(requeued).Count().IsEqualTo(1);
        await Assert.That(requeued[0].Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_Deferring_With_A_Delay_Should_Reenqueue_The_Message_After_The_Delay()
    {
        var (consumer, channel) = CreateConsumer();
        var message = new Message();

        await channel.Writer.WriteAsync(message);
        using var cts = new CancellationTokenSource(ReceiveTimeout);

        var received = await consumer.GetMessagesAsync(cts.Token);
        await consumer.DeferAsync(received[0], TimeSpan.FromMilliseconds(100));

        var requeued = await consumer.GetMessagesAsync(cts.Token);

        await Assert.That(requeued).Count().IsEqualTo(1);
        await Assert.That(requeued[0].Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_Buffer_Size_Is_Three_And_Three_Messages_Are_Queued_Should_Return_Three()
    {
        var (consumer, channel) = CreateConsumer(subscription => subscription.BufferSize = 3);

        for (var i = 0; i < 3; i++)
        {
            await channel.Writer.WriteAsync(new Message());
        }

        using var cts = new CancellationTokenSource(ReceiveTimeout);
        var messages = await consumer.GetMessagesAsync(cts.Token);

        await Assert.That(messages).Count().IsEqualTo(3);
    }
}
