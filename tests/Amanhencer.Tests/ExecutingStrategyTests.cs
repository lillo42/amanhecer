using System;
using System.Collections.Immutable;
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
        var failing = ThrowingPipeline();
        var other = Substitute.For<IPipeline>();

        await Assert.That(async () => await strategy.ExecuteAsync(TestPipelineContext.Create(), [failing, other]))
            .ThrowsExactly<AggregateException>();

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
}
