using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerMessagePumpTests
{
    private readonly IDispatcher _dispatcher;
    private readonly AmanhecerMessagePump _pump;

    public AmanhecerMessagePumpTests()
    {
        _dispatcher = Substitute.For<IDispatcher>();
        var provider = new ServiceCollection()
            .AddSingleton(_dispatcher)
            .AddSingleton(Substitute.For<ILogger<SequentialBatchProcessingStrategy>>())
            .AddSingleton(Substitute.For<ILogger<ParallelBatchProcessingStrategy>>())
            .BuildServiceProvider();
        _pump = new AmanhecerMessagePump(provider, Substitute.For<ILogger<AmanhecerMessagePump>>());
    }

    [Test]
    public async Task When_MessageIsProcessedSuccessfully_Should_AckMessage()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>((object?)null));

        using var cts = new CancellationTokenSource();
        consumer.AckAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).AckAsync(message);
        await consumer.DidNotReceive().NackAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task When_SubscriptionDefinesBatchProcessingStrategy_Should_UseIt()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var messages = new[] { new Message(), new Message() };
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>(messages));

        using var cts = new CancellationTokenSource();
        var strategy = new TestBatchProcessingStrategy((_, _, _, _, _) =>
        {
            cts.Cancel();
            return ValueTask.CompletedTask;
        });
        subscription.BatchProcessingStrategy = strategy;

        await _pump.ExecuteAsync(consumer, cts.Token);

        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(ReferenceEquals(strategy.Messages, messages)).IsTrue();
        await _dispatcher.DidNotReceive()
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().NackAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task When_ProcessingMessage_Should_DispatchWithSubscriptionContext()
    {
        var subscription = CreateSubscription();
        subscription.MessageMapperType = typeof(SomeMessageMapper);
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>((object?)null));

        using var cts = new CancellationTokenSource();
        consumer.AckAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await _dispatcher.Received(1).QueryAsync<object?>(
            message,
            Arg.Is<AmanhecerContext>(c =>
                c.GetMetadata<ISubscription>(MetadataName.Subscription) == subscription &&
                c.GetMetadata<Message>(MetadataName.OriginalMessage) == message &&
                c.GetMetadata<Type>(MetadataName.MessageMapperType) == typeof(SomeMessageMapper) &&
                c.RoutingKey == subscription.ToRoutingKey &&
                c.CorrelationId == message.CorrelationId &&
                c.RequestId == message.Id),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_ProcessingMessage_Should_TagTheActivityWithTheMessagingSystem()
    {
        var subscription = CreateSubscription("rabbitmq");
        subscription.Name = $"messaging-system-{Guid.NewGuid():N}";
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>((object?)null));

        Activity? processed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhecerDiagnostics.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName == $"{subscription.Name} process")
                {
                    processed = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        using var cts = new CancellationTokenSource();
        consumer.AckAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await Assert.That(processed).IsNotNull();
        await Assert.That(processed!.GetTagItem("messaging.system")).IsEqualTo("rabbitmq");
    }

    [Test]
    public async Task When_DispatcherReturnsNack_Should_NackMessage()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(Nack.Instance));

        using var cts = new CancellationTokenSource();
        consumer.NackAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).NackAsync(message);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task When_DispatcherReturnsDefer_Should_DeferMessageWithDelay()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        var delay = TimeSpan.FromSeconds(3);
        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(new Defer(delay)));

        using var cts = new CancellationTokenSource();
        consumer.DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).DeferAsync(message, delay);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().NackAsync(Arg.Any<Message>());
    }

    [Test]
    public async Task When_DispatcherReturnsResolvingConsumerAction_Should_ResolveAndApplyIt()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        var resolving = Substitute.For<IResolvingConsumerAction>();
        resolving
            .ExecuteAsync(message, subscription, _dispatcher, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IConsumerAction>(Nack.Instance));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(resolving));

        using var cts = new CancellationTokenSource();
        consumer.NackAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await resolving.Received(1)
            .ExecuteAsync(message, subscription, _dispatcher, Arg.Any<CancellationToken>());
        await consumer.Received(1).NackAsync(message);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
    }

    [Test]
    public async Task When_DispatcherThrowsNackException_Should_NackMessage()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(Task.FromException<object?>(new NackException())));

        using var cts = new CancellationTokenSource();
        consumer.NackAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).NackAsync(message);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
    }

    [Test]
    public async Task When_DispatcherThrowsDeferException_Should_DeferMessageWithDelay()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        var delay = TimeSpan.FromSeconds(7);
        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(Task.FromException<object?>(new DeferException(delay))));

        using var cts = new CancellationTokenSource();
        consumer.DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).DeferAsync(message, delay);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().NackAsync(Arg.Any<Message>());
    }

    [Test]
    public async Task When_ProcessingFails_Should_ApplyOnErrorAction()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var exception = new InvalidOperationException("Processing failed.");
        Message? failedMessage = null;
        Exception? caughtException = null;
        subscription.OnError = (m, e) =>
        {
            failedMessage = m;
            caughtException = e;
            return Nack.Instance;
        };

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(Task.FromException<object?>(exception)));

        using var cts = new CancellationTokenSource();
        consumer.NackAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).NackAsync(message);
        await Assert.That(failedMessage).IsEqualTo(message);
        await Assert.That(caughtException).IsEqualTo(exception);
    }

    [Test]
    public async Task When_OnErrorActionThrows_Should_NackMessage()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var resolving = Substitute.For<IResolvingConsumerAction>();
        resolving
            .ExecuteAsync(Arg.Any<Message>(), Arg.Any<ISubscription>(), Arg.Any<IDispatcher>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IConsumerAction>(
                Task.FromException<IConsumerAction>(new InvalidOperationException("Error action failed."))));

        subscription.OnError = (_, _) => resolving;

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(
                Task.FromException<object?>(new InvalidOperationException("Processing failed."))));

        using var cts = new CancellationTokenSource();
        consumer.NackAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(1).NackAsync(message);
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task When_ReceivingMessagesFails_Should_RetryAndKeepPumping()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        var message = new Message();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(
                new ValueTask<Message[]>(Task.FromException<Message[]>(new InvalidOperationException())),
                new ValueTask<Message[]>([message]));

        _dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>((object?)null));

        using var cts = new CancellationTokenSource();
        consumer.AckAsync(Arg.Any<Message>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.Received(2).GetMessagesAsync(Arg.Any<CancellationToken>());
        await consumer.Received(1).AckAsync(message);
    }

    [Test]
    public async Task When_NoMessagesAreReceived_Should_NotDispatch()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        using var cts = new CancellationTokenSource();
        consumer.GetMessagesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return new ValueTask<Message[]>([]);
            });

        await _pump.ExecuteAsync(consumer, cts.Token);

        await _dispatcher.DidNotReceive()
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
        await consumer.DidNotReceive().AckAsync(Arg.Any<Message>());
    }

    [Test]
    public async Task When_CancellationIsRequested_Should_StopWithoutPolling()
    {
        var subscription = CreateSubscription();
        var consumer = Substitute.For<IConsumer>();
        consumer.Subscription.Returns(subscription);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _pump.ExecuteAsync(consumer, cts.Token);

        await consumer.DidNotReceive().GetMessagesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive()
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
    }

    private static TestSubscription CreateSubscription(string messagingSystem = "amanhecer")
    {
        return new TestSubscription(messagingSystem)
        {
            Name = "some-subscription",
            NoMessageDelay = TimeSpan.Zero,
            FailureDelay = TimeSpan.Zero,
            ReceiveMessageTimeout = Timeout.InfiniteTimeSpan
        };
    }

    private sealed class TestSubscription(string messagingSystem) : Subscription("some.routing.key")
    {
        public override string MessagingSystem { get; } = messagingSystem;
    }

    private sealed class SomeMessageMapper : IMessageMapper
    {
        public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
        {
            return new ValueTask<Message>(new Message());
        }

        public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
        {
            return new ValueTask<object>(new object());
        }
    }

    private sealed class TestBatchProcessingStrategy(
        Func<IServiceProvider, ISubscription, IConsumer, Message[], CancellationToken, ValueTask> executeAsync)
        : IBatchProcessingStrategy
    {
        public int CallCount { get; private set; }

        public Message[]? Messages { get; private set; }

        public ValueTask ExecuteAsync(IServiceProvider provider,
            ISubscription subscription,
            IConsumer consumer,
            Message[] messages,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Messages = messages;
            return executeAsync(provider, subscription, consumer, messages, cancellationToken);
        }
    }
}
