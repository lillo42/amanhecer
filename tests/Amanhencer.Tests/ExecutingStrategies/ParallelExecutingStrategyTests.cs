using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Amanhencer.Tests.ExecutingStrategies;

public class ParallelExecutingStrategyTests
{
    private readonly ParallelExecutingStrategy _strategy = new(new ParallelOptions
    {
        MaxDegreeOfParallelism = 3
    }, new AmanhencerPipelineContextAccessor(), new NullLogger<ParallelExecutingStrategy>());

    [Test]
    public async Task When_ExecuteAsync_WithEmptyPipelineList_Should_DoNothing()
    {
        var context = Substitute.For<IPipelineContext>();
        var pipeline = Substitute.For<IPipeline>();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, ImmutableList<IPipeline>.Empty))
            .ThrowsNothing();

        await pipeline
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteWhenHasOnlyPipeline()
    {
        var context = Substitute.For<IPipelineContext>();
        var pipeline = Substitute.For<IPipeline>();
        var pipelines = ImmutableList<IPipeline>.Empty.Add(pipeline);

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        await pipeline
            .Received(1)
            .ExecuteAsync(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_OnePipelineNotSilentFail()
    {
        var context = Substitute.For<IPipelineContext>();
        var pipeline = Substitute.For<IPipeline>();

        pipeline
            .ExecuteAsync(context)
            .Throws(new Exception());

        var pipelines = ImmutableList<IPipeline>.Empty.Add(pipeline);

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .Throws<Exception>();

        await pipeline
            .Received(1)
            .ExecuteAsync(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PropagateOriginalExceptionWhenSinglePipelineFails()
    {
        var context = Substitute.For<IPipelineContext>();
        var pipeline = Substitute.For<IPipeline>();

        pipeline
            .ExecuteAsync(context)
            .Throws(new InvalidOperationException("boom"));

        var pipelines = ImmutableList<IPipeline>.Empty.Add(pipeline);

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsExactly<InvalidOperationException>();
    }


    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteInSequenceWhenExecuteInPipeline()
    {
        var context = Substitute.For<IPipelineContext>();
        var pipelines = Enumerable.Range(0, 10)
            .Select(_ =>
            {
                var pipeline = Substitute.For<IPipeline>();

                pipeline.ExecuteAsync(Arg.Any<IPipelineContext>())
                    .Returns(_ => new ValueTask(Delay()));

                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();


        foreach (var pipeline in pipelines)
        {
            await pipeline
                .Received(1)
                .ExecuteAsync(Arg.Any<IPipelineContext>());
        }

        static async Task Delay()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WithMultiplePipelines_Should_ExecuteEachWithDistinctClonedContext()
    {
        var context = Substitute.For<IPipelineContext>();
        context.DeepClone().Returns(_ => Substitute.For<IPipelineContext>());

        var usedContexts = new ConcurrentBag<IPipelineContext>();
        var pipelines = Enumerable.Range(0, 3)
            .Select(_ =>
            {
                var pipeline = Substitute.For<IPipeline>();
                pipeline.ExecuteAsync(Arg.Any<IPipelineContext>())
                    .Returns(callInfo =>
                    {
                        usedContexts.Add(callInfo.Arg<IPipelineContext>());
                        return new ValueTask();
                    });

                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        context.Received(pipelines.Count).DeepClone();

        await Assert.That(usedContexts)
            .Count().IsEqualTo(pipelines.Count)
            .And.DoesNotContain(context);
        await Assert.That(usedContexts.Distinct()).Count().IsEqualTo(pipelines.Count);

        foreach (var pipeline in pipelines)
        {
            await pipeline
                .DidNotReceive()
                .ExecuteAsync(context);
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WithOneFailingPipeline_Should_ExecuteAllPipelinesAndThrowAggregateException()
    {
        var context = Substitute.For<IPipelineContext>();
        context.DeepClone().Returns(_ => Substitute.For<IPipelineContext>());

        var expected = new InvalidOperationException("boom");
        var pipelines = Enumerable.Range(0, 3)
            .Select(i =>
            {
                var pipeline = Substitute.For<IPipeline>();
                if (i == 1)
                {
                    pipeline
                        .ExecuteAsync(Arg.Any<IPipelineContext>())
                        .Throws(expected);
                }

                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .Throws<AggregateException>()
            .And.Member(x => x.InnerExceptions.Count, y => y.IsEqualTo(1))
            .And.Member(x => x.InnerExceptions[0], y => y.IsSameReferenceAs(expected));

        foreach (var pipeline in pipelines)
        {
            await pipeline
                .Received(1)
                .ExecuteAsync(Arg.Any<IPipelineContext>());
        }
    }

    [Test]
    public async Task When_ExecuteAsync_Should_NotSilentFailWithMultiplePipelines()
    {
        var context = Substitute.For<IPipelineContext>();

        var pipelines = Enumerable.Range(0, 2)
            .Select(_ =>
            {
                var pipeline = Substitute.For<IPipeline>();
                pipeline.ExecuteAsync(Arg.Any<IPipelineContext>())
                    .Throws(new Exception());
                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .Throws<AggregateException>()
            .And.Member(x => x.InnerExceptions.Count, y => y.IsEqualTo(pipelines.Count));
    }
}
