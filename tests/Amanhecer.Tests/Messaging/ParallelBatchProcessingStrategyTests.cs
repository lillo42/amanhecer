using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class ParallelBatchProcessingStrategyTests
{
    [Test]
    public async Task When_ProcessPartitionsSequentiallyIsFalse_Should_ProcessEachMessageOnce()
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var provider = new ServiceCollection()
            .AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<ILogger<ParallelBatchProcessingStrategy>>())
            .AddSingleton(Substitute.For<ILogger<SequentialBatchProcessingStrategy>>())
            .BuildServiceProvider();

        var strategy = new ParallelBatchProcessingStrategy
        {
            ProcessPartitionsSequentially = false,
            ParallelExecutionOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 1,
            }
        };

        var subscription = new TestSubscription
        {
            BatchProcessingTimeout = Timeout.InfiniteTimeSpan,
            MessageProcessingTimeout = Timeout.InfiniteTimeSpan,
        };

        var consumer = Substitute.For<IConsumer>();
        var messages = new[]
        {
            new Message { Id = "message-1" },
            new Message { Id = "message-2" },
        };

        dispatcher
            .QueryAsync<object?>(Arg.Any<object>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>((object?)null));

        await strategy.ExecuteAsync(provider, subscription, consumer, messages, CancellationToken.None);

        await dispatcher.Received(1)
            .QueryAsync<object?>(messages[0], Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
        await dispatcher.Received(1)
            .QueryAsync<object?>(messages[1], Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
        await consumer.Received(1).AckAsync(messages[0]);
        await consumer.Received(1).AckAsync(messages[1]);
        await consumer.DidNotReceive().NackAsync(Arg.Any<Message>());
        await consumer.DidNotReceive().DeferAsync(Arg.Any<Message>(), Arg.Any<TimeSpan>());
    }

    private sealed class TestSubscription() : Subscription("some.routing.key")
    {
        public override string MessagingSystem => "amanhecer";
    }
}
