using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.ExecutingStrategies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TUnit.Assertions.Enums;

namespace Amanhecer.Tests.ExecutingStrategies;

public class SequenceExecutingStrategyTests
{
    private readonly SequenceExecutingStrategy _strategy = new(
        new AmanhecerPipelineContextAccessor(),
        new NullLogger<SequenceExecutingStrategy>());

    [Test]
    public async Task When_ExecuteAsync_WithEmptyPipelineList_Should_DoNothing()
    {
        var context = new AmanhecerContext();
        var pipeline = Substitute.For<IPipeline>();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, ImmutableList<IPipeline>.Empty))
            .ThrowsNothing();

        await pipeline
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteWhenHasOnlyPipeline()
    {
        var context = new AmanhecerContext();
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
        var context = new AmanhecerContext();
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
        var context = new AmanhecerContext();
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
        var context = new AmanhecerContext();
        var counter = 0;

        var pipelines = Enumerable.Range(0, 2)
                .Select(_ =>
                {
                    var pipeline = Substitute.For<IPipeline>();

                    pipeline.ExecuteAsync(Arg.Any<AmanhecerContext>())
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
                .ExecuteAsync(Arg.Any<AmanhecerContext>());
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WithMultiplePipelines_Should_ExecuteEachWithDistinctClonedContext()
    {
        var context = new AmanhecerContext { RoutingKey = "some.key" };
        var usedContexts = new List<AmanhecerContext>();

        var pipelines = Enumerable.Range(0, 3)
            .Select(_ =>
            {
                var pipeline = Substitute.For<IPipeline>();
                pipeline.ExecuteAsync(Arg.Any<AmanhecerContext>())
                    .Returns(callInfo =>
                    {
                        usedContexts.Add(callInfo.Arg<AmanhecerContext>());
                        return new ValueTask();
                    });

                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        await Assert.That(usedContexts)
            .Count().IsEqualTo(pipelines.Count)
            .And.DoesNotContain(context);
        await Assert.That(usedContexts.Distinct()).Count().IsEqualTo(pipelines.Count);
        await Assert.That(usedContexts.All(x => x.RoutingKey == context.RoutingKey)).IsTrue();

        foreach (var pipeline in pipelines)
        {
            await pipeline
                .DidNotReceive()
                .ExecuteAsync(context);
        }
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_ExecuteAsync_WithMultiplePipelines_Should_ExecuteSequentially()
    {
        var context = new AmanhecerContext();

        var events = new List<string>();

        var first = Substitute.For<IPipeline>();
        first.ExecuteAsync(Arg.Any<AmanhecerContext>())
            .Returns(_ => new ValueTask(FirstPipeline()));

        var second = Substitute.For<IPipeline>();
        second.ExecuteAsync(Arg.Any<AmanhecerContext>())
            .Returns(_ =>
            {
                events.Add("second-started");
                events.Add("second-completed");
                return new ValueTask();
            });

        var pipelines = ImmutableList.Create(first, second);

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .ThrowsNothing();

        await Assert.That(events)
            .IsEquivalentTo(
                ["first-started", "first-completed", "second-started", "second-completed"],
                CollectionOrdering.Matching);

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
        var context = new AmanhecerContext();

        var expected = new InvalidOperationException("boom");
        var pipelines = Enumerable.Range(0, 3)
            .Select(i =>
            {
                var pipeline = Substitute.For<IPipeline>();
                if (i == 1)
                {
                    pipeline
                        .ExecuteAsync(Arg.Any<AmanhecerContext>())
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
                .ExecuteAsync(Arg.Any<AmanhecerContext>());
        }
    }

    [Test]
    public async Task When_ExecuteAsync_Should_NotSilentFailWithMultiplePipelines()
    {
        var context = new AmanhecerContext();

        var pipelines = Enumerable.Range(0, 2)
            .Select(_ =>
            {
                var pipeline = Substitute.For<IPipeline>();
                pipeline.ExecuteAsync(Arg.Any<AmanhecerContext>())
                    .Throws(new Exception());
                return pipeline;
            })
            .ToImmutableList();

        await Assert.That(async () => await _strategy.ExecuteAsync(context, pipelines))
            .Throws<AggregateException>()
            .And.Member(x => x.InnerExceptions.Count, y => y.IsEqualTo(pipelines.Count));
    }
}
