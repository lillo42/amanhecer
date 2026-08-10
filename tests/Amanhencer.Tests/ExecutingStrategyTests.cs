using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using NSubstitute;

namespace Amanhencer.Tests;

public class ExecutingStrategyTests
{
    private static IPipeline ThrowingPipeline()
    {
        var pipeline = Substitute.For<IPipeline>();
        pipeline.ExecuteAsync(Arg.Any<IPipelineContext>())
            .Returns(_ => throw new InvalidOperationException("Pipeline failed."));
        return pipeline;
    }

    [Test]
    public async Task Sequence_NoPipelines_DoesNothing()
    {
        var strategy = new SequenceExecutingStrategy();

        await Assert.That(async () =>
                await strategy.ExecuteAsync(TestPipelineContext.Create(), ImmutableList<IPipeline>.Empty))
            .ThrowsNothing();
    }

    [Test]
    public async Task Sequence_SinglePipeline_ExecutesWithSameContext()
    {
        var strategy = new SequenceExecutingStrategy();
        var context = TestPipelineContext.Create();
        var pipeline = Substitute.For<IPipeline>();

        await strategy.ExecuteAsync(context, [pipeline]);

        _ = pipeline.Received(1).ExecuteAsync(Arg.Is<IPipelineContext>(c => ReferenceEquals(c, context)));
    }

    [Test]
    public async Task Sequence_MultiplePipelines_ExecuteInOrder()
    {
        var strategy = new SequenceExecutingStrategy();
        var first = Substitute.For<IPipeline>();
        var second = Substitute.For<IPipeline>();

        await strategy.ExecuteAsync(TestPipelineContext.Create(), [first, second]);

        Received.InOrder(() =>
        {
            _ = first.ExecuteAsync(Arg.Any<IPipelineContext>());
            _ = second.ExecuteAsync(Arg.Any<IPipelineContext>());
        });
    }

    [Test]
    public async Task Sequence_MultiplePipelines_ClonesContextPerPipeline()
    {
        var strategy = new SequenceExecutingStrategy();
        var context = TestPipelineContext.Create();
        var first = Substitute.For<IPipeline>();
        var second = Substitute.For<IPipeline>();
        first.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
            .Do(ci => ci.Arg<IPipelineContext>().Metadata["first"] = true);
        second.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
            .Do(ci => ci.Arg<IPipelineContext>().Metadata["second"] = true);

        await strategy.ExecuteAsync(context, [first, second]);

        _ = first.Received(1).ExecuteAsync(Arg.Is<IPipelineContext>(c => !ReferenceEquals(c, context)));
        _ = second.Received(1).ExecuteAsync(Arg.Is<IPipelineContext>(c => !ReferenceEquals(c, context)));
        await Assert.That(context.Metadata).IsEmpty();
    }

    [Test]
    public async Task Sequence_MultiplePipelines_Failure_ThrowsAggregateException_AndRunsRemaining()
    {
        var strategy = new SequenceExecutingStrategy();
        var firstFailing = ThrowingPipeline();
        var secondFailing = ThrowingPipeline();
        var other = Substitute.For<IPipeline>();

        var exception = await Assert.That(async () =>
                await strategy.ExecuteAsync(TestPipelineContext.Create(), [firstFailing, secondFailing, other]))
            .ThrowsExactly<AggregateException>();

        // All pipeline failures are aggregated, not just the first one.
        await Assert.That(exception!.InnerExceptions.Count).IsEqualTo(2);
        await Assert.That(exception.InnerExceptions.All(e => e is InvalidOperationException)).IsTrue();
        _ = other.Received(1).ExecuteAsync(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task Sequence_SinglePipeline_Failure_PropagatesOriginalException()
    {
        var strategy = new SequenceExecutingStrategy();
        var failing = ThrowingPipeline();

        await Assert.That(async () => await strategy.ExecuteAsync(TestPipelineContext.Create(), [failing]))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Parallel_NoPipelines_DoesNothing()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());

        await Assert.That(async () =>
                await strategy.ExecuteAsync(TestPipelineContext.Create(), ImmutableList<IPipeline>.Empty))
            .ThrowsNothing();
    }

    [Test]
    public async Task Parallel_SinglePipeline_ExecutesWithSameContext()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());
        var context = TestPipelineContext.Create();
        var pipeline = Substitute.For<IPipeline>();

        await strategy.ExecuteAsync(context, [pipeline]);

        _ = pipeline.Received(1).ExecuteAsync(Arg.Is<IPipelineContext>(c => ReferenceEquals(c, context)));
    }

    [Test]
    public async Task Parallel_MultiplePipelines_ExecuteAll()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());
        var first = Substitute.For<IPipeline>();
        var second = Substitute.For<IPipeline>();

        await strategy.ExecuteAsync(TestPipelineContext.Create(), [first, second]);

        _ = first.Received(1).ExecuteAsync(Arg.Any<IPipelineContext>());
        _ = second.Received(1).ExecuteAsync(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task Parallel_MultiplePipelines_Failure_PropagatesException_AndRunsRemaining()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());
        var other = Substitute.For<IPipeline>();
        var failing = ThrowingPipeline();

        await Assert.That(async () => await strategy.ExecuteAsync(TestPipelineContext.Create(), [other, failing]))
            .ThrowsExactly<InvalidOperationException>();

        _ = other.Received(1).ExecuteAsync(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task Parallel_MultiplePipelines_HonorsMaxDegreeOfParallelism()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions { MaxDegreeOfParallelism = 2 });
        var current = 0;
        var max = 0;

        IPipeline BlockingPipeline()
        {
            var pipeline = Substitute.For<IPipeline>();
            pipeline.ExecuteAsync(Arg.Any<IPipelineContext>()).Returns(_ => TrackConcurrency());
            return pipeline;
        }

        async ValueTask TrackConcurrency()
        {
            var running = Interlocked.Increment(ref current);
            int observed;
            do
            {
                observed = max;
            } while (running > observed && Interlocked.CompareExchange(ref max, running, observed) != observed);

            await Task.Delay(25);
            Interlocked.Decrement(ref current);
        }

        var pipelines = ImmutableList.Create(BlockingPipeline(), BlockingPipeline(), BlockingPipeline(), BlockingPipeline());

        await strategy.ExecuteAsync(TestPipelineContext.Create(), pipelines);

        foreach (var pipeline in pipelines)
        {
            _ = pipeline.Received(1).ExecuteAsync(Arg.Any<IPipelineContext>());
        }

        await Assert.That(max).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task Parallel_MultiplePipelines_ClonesContextPerPipeline()
    {
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());
        var context = TestPipelineContext.Create();
        var seen = new ConcurrentBag<IPipelineContext>();

        IPipeline CapturingPipeline()
        {
            var pipeline = Substitute.For<IPipeline>();
            pipeline.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
                .Do(ci => seen.Add(ci.Arg<IPipelineContext>()));
            return pipeline;
        }

        await strategy.ExecuteAsync(context, [CapturingPipeline(), CapturingPipeline()]);

        await Assert.That(seen.Count).IsEqualTo(2);
        await Assert.That(seen.Distinct().Count()).IsEqualTo(2);
        await Assert.That(seen.Any(c => ReferenceEquals(c, context))).IsFalse();
    }

    [Test]
    public async Task Parallel_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var strategy = new ParallelExecutingStrategy(new ParallelOptions { CancellationToken = cts.Token });
        var first = Substitute.For<IPipeline>();
        var second = Substitute.For<IPipeline>();

        await Assert.That(async () => await strategy.ExecuteAsync(TestPipelineContext.Create(), [first, second]))
            .Throws<OperationCanceledException>();
    }
}
