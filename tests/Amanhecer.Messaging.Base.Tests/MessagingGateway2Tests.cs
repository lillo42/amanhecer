using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Base.Tests.Extensions;

namespace Amanhecer.Messaging.Base.Tests;

public abstract class MessagingGateway2Tests<TGateway>
    where TGateway : class, IGateway
{
    protected TGateway Gateway { get; private set; } = null!;

    protected abstract TGateway CreateGateway();

    protected abstract IPublication CreatePublication(ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        [CallerMemberName] string? routingKey = null);

    protected virtual IProducer CreateProducer()
    {
        var producers = Gateway.CreateProducers();
        return producers.First().Value;
    }

    protected abstract ISubscription CreateSubscription(
        ProvisionerStrategy strategy = ProvisionerStrategy.CreateOrUpdate,
        [CallerMemberName] string? routingKey = null);

    protected virtual IConsumer CreateConsumer()
    {
        return Gateway.CreateConsumer(Gateway.Subscriptions.First());
    }

    protected virtual Message CreateMessage(Action<MessageBuilder> builder)
    {
        var messageBuilder = new MessageBuilder();
        builder(messageBuilder);
        return messageBuilder.Build();
    }

    [RequiresUnreferencedCode("")]
    protected virtual async Task AssertMessageAsync(Message expected, Message received)
    {
        await Assert.That(received)
            .Member(x => x.ContentType, x => x.IsEqualTo(expected.ContentType))
            .And.Member(x => x.CorrelationId, x => x.IsEqualTo(expected.CorrelationId))
            .And.Member(x => x.DataSchema, x => x.IsEqualTo(expected.DataSchema))
            .And.Member(x => x.DataRef, x => x.IsEqualTo(expected.DataRef))
            .And.Member(x => x.Id, x => x.IsEqualTo(expected.Id))
            .And.Member(x => x.PartitionKey, x => x.IsEqualTo(expected.PartitionKey))
            .And.Member(x => x.ReplyTo, x => x.IsEqualTo(expected.ReplyTo))
            .And.Member(x => x.Subject, x => x.IsEqualTo(expected.Subject))
            .And.Member(x => x.Source, x => x.IsEqualTo(expected.Source))
            .And.Member(x => x.Time, x => x.IsEqualTo(expected.Time))
            .And.Member(x => x.TraceParent, x => x.IsEqualTo(expected.TraceParent));

        if (expected.Baggage != null)
        {
            await Assert.That(received.Baggage!)
                .IsNotNull()
                .IsEquivalentTo(expected.Baggage.ToString());
        }
        else
        {
            await Assert.That(received.Baggage).IsNull();
        }

        if (expected.TraceState != null)
        {
            await Assert.That(received.TraceState!)
                .IsNotNull()
                .IsEquivalentTo(expected.TraceState.ToString());
        }
        else
        {
            await Assert.That(received.TraceState).IsNull();
        }


        foreach (var header in expected.Headers)
        {
            var headerValue = header.Value switch
            {
                DateTime t => t.ToString("O"),
                DateTimeOffset t => t.ToString("O"),
                _ => header.Value!.ToString()
            };
            
            await Assert.That(received.Headers)
                .ContainsKeyWithValue(header.Key, headerValue);
        }
    }

    protected virtual TimeSpan MessagePropagateDelay { get; } = TimeSpan.FromSeconds(5);

    protected virtual async Task WaitMessageToBePropagated()
    {
        if (MessagePropagateDelay != TimeSpan.Zero)
        {
            await Task.Delay(MessagePropagateDelay);
        }
    }

    protected virtual Task CleanupAsync()
    {
        return Task.CompletedTask;
    }

    [Before(HookType.Test)]
    public void Setup(TestContext context)
    {
        Gateway = CreateGateway();
    }

    [After(HookType.Test)]
    public async Task AfterTests()
    {
        await CleanupAsync();

        if (Gateway is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Gateway is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Gateway = null!;
    }

    [Test]
    public async Task When_A_Consumer_Receives_Multiple_Messages_Should_Return_All()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();

        var messages = new[]
        {
            CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString())),
            CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString())),
            CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString())),
            CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString())),
        };

        await messages.EachAsync(async m => await producer
            .ProduceAsync(m, Gateway.Publications.First(), new AmanhecerContext()));

        await WaitMessageToBePropagated();

        var subs = Gateway.Subscriptions.First();
        var timeout = MessagePropagateDelay > subs.ReceiveMessageTimeout
            ? MessagePropagateDelay
            : subs.ReceiveMessageTimeout;
        var receivedMessages = new List<Message>(messages.Length);
        var counter = 0;
        while (counter < messages.Length)
        {
            using var cts = new CancellationTokenSource(timeout);
            var received = await consumer.GetMessagesAsync(cts.Token);
            await received.EachAsync(async message =>
            {
                var expected = await Assert.That(messages).HasSingleItem(m => m.Id == message.Id);
                await AssertMessageAsync(expected, message);
            });

            receivedMessages.AddRange(received);
            counter += received.Length;
        }

        var receivedIds = receivedMessages.Select(x => x.Id).ToArray();
        await Assert.That(receivedIds).Count().IsEqualTo(messages.Length);
        foreach (var message in messages)
        {
            await Assert.That(receivedIds).Contains(message.Id);
        }
    }
}