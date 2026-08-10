using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Amanhencer.Tests.ExecutingStrategies;

public class SequenceExecutingStrategyTests
{
    private readonly SequenceExecutingStrategy _strategy = new(new NullLogger<SequenceExecutingStrategy>());

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
        var counter = 0;

        var pipelines = Enumerable.Range(0, 2)
                .Select(_ =>
                {
                    var pipeline = Substitute.For<IPipeline>();

                    pipeline.ExecuteAsync(Arg.Any<IPipelineContext>())
                        .Returns(_ =>
                        {
                            counter++;
                            return new ValueTask();
                        });

                    return pipeline;
                })
                .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        await Assert.That(counter).IsEqualTo(pipelines.Count);

        foreach (var pipeline in pipelines)
        {
            await pipeline
                .Received(1)
                .ExecuteAsync(Arg.Any<IPipelineContext>());
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WithMultiplePipelines_Should_ExecuteEachWithDistinctClonedContext()
    {
        var context = Substitute.For<IPipelineContext>();
        var clonedContexts = Enumerable.Range(0, 3)
            .Select(_ => Substitute.For<IPipelineContext>())
            .ToList();

        var cloneIndex = 0;
        context.DeepClone().Returns(_ => clonedContexts[cloneIndex++]);

        var pipelines = Enumerable.Range(0, 3)
            .Select(_ => Substitute.For<IPipeline>())
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        context.Received(pipelines.Count).DeepClone();

        for (var i = 0; i < pipelines.Count; i++)
        {
            await pipelines[i]
                .Received(1)
                .ExecuteAsync(clonedContexts[i]);

            await pipelines[i]
                .DidNotReceive()
                .ExecuteAsync(context);
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WithMultiplePipelines_Should_ExecuteSequentially()
    {
        var context = Substitute.For<IPipelineContext>();
        context.DeepClone().Returns(_ => Substitute.For<IPipelineContext>());

        var events = new List<string>();

        var first = Substitute.For<IPipeline>();
        first.ExecuteAsync(Arg.Any<IPipelineContext>())
            .Returns(_ => new ValueTask(FirstPipeline()));

        var second = Substitute.For<IPipeline>();
        second.ExecuteAsync(Arg.Any<IPipelineContext>())
            .Returns(_ =>
            {
                events.Add("second-started");
                events.Add("second-completed");
                return new ValueTask();
            });

        var pipelines = ImmutableList.Create(first, second);

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        await Assert.That(events.Count).IsEqualTo(4);
        await Assert.That(events[0]).IsEqualTo("first-started");
        await Assert.That(events[1]).IsEqualTo("first-completed");
        await Assert.That(events[2]).IsEqualTo("second-started");
        await Assert.That(events[3]).IsEqualTo("second-completed");

        async Task FirstPipeline()
        {
            events.Add("first-started");
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            events.Add("first-completed");
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
