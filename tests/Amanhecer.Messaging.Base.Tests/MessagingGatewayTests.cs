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

public abstract class MessagingGatewayTests<TGateway>
    where TGateway : class, IGateway
{
    protected TGateway Gateway { get; private set; } = null!;

    protected virtual TimeSpan NoMessageTimeout { get; } = TimeSpan.FromMilliseconds(500);

    protected virtual bool SupportsPostingWithoutBrokerFailureTest => true;

    protected virtual bool SupportsDeadLetterAfterTooManyRequeuesTest => false;

    protected virtual int DeadLetterAfterTooManyRequeuesCount => 3;

    protected virtual TimeSpan DeadLetterReceiveTimeout => TimeSpan.FromSeconds(10);

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

    protected virtual Message CloneMessage(Message message)
    {
        return new Message
        {
            Baggage = message.Baggage == null ? null : new Baggage(message.Baggage),
            ContentType = message.ContentType,
            CorrelationId = message.CorrelationId,
            DataRef = message.DataRef,
            DataSchema = message.DataSchema,
            Headers = new Dictionary<string, object?>(message.Headers),
            Id = message.Id,
            Metadata = new Dictionary<string, object?>(message.Metadata),
            PartitionKey = message.PartitionKey,
            Payload = message.Payload,
            ReplyTo = message.ReplyTo,
            Source = message.Source,
            SpecVersion = message.SpecVersion,
            Subject = message.Subject,
            Time = message.Time,
            TraceParent = message.TraceParent,
            TraceState = message.TraceState == null ? null : new TraceState(message.TraceState),
            Type = message.Type
        };
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
            .And.Member(x => x.Time, x => x.IsEqualTo(expected.Time));

        if (!string.IsNullOrEmpty(expected.TraceParent))
        {
            await Assert.That(received.TraceParent).IsEqualTo(expected.TraceParent);
        }

        if (expected.Baggage != null)
        {
            await Assert.That(received.Baggage).IsNotNull();
            foreach (var pairValue in expected.Baggage)
            {
                await Assert.That(received.Baggage!).ContainsKeyWithValue(pairValue.Key, pairValue.Value);
            }
        }

        if (expected.TraceState != null)
        {
            await Assert.That(received.TraceState?.ToString()).IsEqualTo(expected.TraceState.ToString());
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

    protected virtual async Task<Message[]> ReceiveManyAsync(IConsumer consumer, int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var receivedMessages = new List<Message>(count);

        while (receivedMessages.Count < count && !cts.IsCancellationRequested)
        {
            Message[] received;
            try
            {
                received = await consumer.GetMessagesAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (received.Length == 0)
            {
                continue;
            }

            receivedMessages.AddRange(received);
        }

        return [.. receivedMessages.Take(count)];
    }

    protected virtual async Task AssertNoMessageAsync(IConsumer consumer, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var received = await consumer.GetMessagesAsync(cts.Token);
            await Assert.That(received).IsEmpty();
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected virtual async Task WaitMessageToBePropagated()
    {
        if (MessagePropagateDelay != TimeSpan.Zero)
        {
            await Task.Delay(MessagePropagateDelay);
        }
    }

    protected virtual Task<Message?> ReceiveFromDeadLetterQueueAsync(TimeSpan timeout)
    {
        return Task.FromResult<Message?>(null);
    }

    protected virtual Task PrepareDeadLetterInfrastructureAsync()
    {
        return Task.CompletedTask;
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
    public async Task When_Producing_A_Message_Should_Be_Received()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));
        var expected = CloneMessage(message);

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        var received = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(received).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, received[0]);
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
        var expectedMessages = messages.Select(CloneMessage).ToArray();

        await messages.EachAsync(async m => await producer
            .ProduceAsync(m, Gateway.Publications.First(), new AmanhecerContext()));

        await WaitMessageToBePropagated();

        var subs = Gateway.Subscriptions.First();
        var timeout = MessagePropagateDelay > subs.ReceiveMessageTimeout
            ? MessagePropagateDelay
            : subs.ReceiveMessageTimeout;
        var receivedMessages = await ReceiveManyAsync(consumer, messages.Length, timeout);

        await receivedMessages.EachAsync(async message =>
        {
            var expected = await Assert.That(expectedMessages).HasSingleItem(m => m.Id == message.Id);
            await AssertMessageAsync(expected, message);
        });

        var receivedIds = receivedMessages.Select(x => x.Id).ToArray();
        await Assert.That(receivedIds).Count().IsEqualTo(messages.Length);
        foreach (var message in messages)
        {
            await Assert.That(receivedIds).Contains(message.Id);
        }
    }

    [Test]
    public async Task When_Acking_A_Message_Should_Not_Be_Redelivered()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));
        var expected = CloneMessage(message);

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        var received = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(received).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, received[0]);

        await consumer.AckAsync(received[0]);

        await AssertNoMessageAsync(consumer, NoMessageTimeout);
    }

    [Test]
    public async Task When_Nacking_A_Message_Should_Not_Be_Redelivered()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));
        var expected = CloneMessage(message);

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        var received = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(received).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, received[0]);

        await consumer.NackAsync(received[0]);

        await AssertNoMessageAsync(consumer, NoMessageTimeout);
    }

    [Test]
    public async Task When_Deferring_A_Message_Should_Be_Redelivered()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));
        var expected = CloneMessage(message);

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        var received = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(received).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, received[0]);

        await consumer.DeferAsync(received[0], TimeSpan.FromMilliseconds(50));

        var redelivered = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(redelivered).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, redelivered[0]);
    }

    [Test]
    public async Task When_Producing_A_Message_With_Trace_Context_Should_Propagate()
    {
        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await PrepareDeadLetterInfrastructureAsync();
        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x =>
        {
            x.SetPayload(Uuid.NewGuid().ToString());
            x.SetTraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
            x.SetTraceState(TraceState.FromString("congo=t61rcWkgMzE"));
            x.SetBaggage(Baggage.FromString("userId=alice,serverNode=DF28"));
        });
        var expected = CloneMessage(message);

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        var received = await ReceiveManyAsync(consumer, 1, timeout);
        await Assert.That(received).Count().IsEqualTo(1);
        await AssertMessageAsync(expected, received[0]);
    }

    [Test]
    public async Task When_Infrastructure_Is_Missing_And_Strategy_Is_Assume_Should_Throw()
    {
        Gateway.Publications = [CreatePublication(ProvisionerStrategy.Assume)];
        Gateway.Subscriptions = [CreateSubscription(ProvisionerStrategy.Assume)];

        await Assert.That(async () =>
            {
                var producer = CreateProducer();
                var consumer = CreateConsumer();
                var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));

                await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());
                await WaitMessageToBePropagated();
                await ReceiveManyAsync(consumer, 1, Gateway.Subscriptions.First().ReceiveMessageTimeout);
            })
            .Throws<Exception>();
    }

    [Test]
    public async Task When_Infrastructure_Is_Missing_And_Strategy_Is_Validate_Should_Throw()
    {
        Gateway.Publications = [CreatePublication(ProvisionerStrategy.Validate)];
        Gateway.Subscriptions = [CreateSubscription(ProvisionerStrategy.Validate)];

        await Assert.That(async () => await Gateway.ProvisionerAsync())
            .Throws<Exception>();
    }

    [Test]
    public async Task When_Multiple_Threads_Try_To_Post_A_Message_At_The_Same_Time_Should_Not_Throw()
    {
        Gateway.Publications = [CreatePublication()];

        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));
                await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());
            }));

        await Task.WhenAll(tasks);
    }

    [Test]
    public async Task When_Posting_A_Message_But_No_Broker_Created_Should_Throw()
    {
        if (!SupportsPostingWithoutBrokerFailureTest)
        {
            Skip.Test($"{typeof(TGateway).Name} does not support the no-broker-created failure contract.");
        }

        Gateway.Publications = [CreatePublication()];

        var producer = CreateProducer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));

        await Assert.That(async () =>
                await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext()))
            .Throws<Exception>();
    }

    [Test]
    public async Task When_Requeuing_A_Message_Too_Many_Times_Should_Move_To_Dead_Letter_Queue()
    {
        if (!SupportsDeadLetterAfterTooManyRequeuesTest)
        {
            Skip.Test($"{typeof(TGateway).Name} does not support the dead-letter-after-requeue contract.");
        }

        Gateway.Publications = [CreatePublication()];
        Gateway.Subscriptions = [CreateSubscription()];

        await PrepareDeadLetterInfrastructureAsync();
        await Gateway.ProvisionerAsync();

        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = CreateMessage(x => x.SetPayload(Uuid.NewGuid().ToString()));

        await producer.ProduceAsync(message, Gateway.Publications.First(), new AmanhecerContext());

        await WaitMessageToBePropagated();

        var timeout = Gateway.Subscriptions.First().ReceiveMessageTimeout;
        for (var i = 0; i < DeadLetterAfterTooManyRequeuesCount; i++)
        {
            var received = await ReceiveManyAsync(consumer, 1, timeout);
            await Assert.That(received).Count().IsEqualTo(1);
            await consumer.DeferAsync(received[0], TimeSpan.FromMilliseconds(50));
        }

        var deadLettered = await ReceiveFromDeadLetterQueueAsync(DeadLetterReceiveTimeout);

        await Assert.That(deadLettered).IsNotNull();
        await Assert.That(deadLettered!.Id).IsEqualTo(message.Id);
    }
}
