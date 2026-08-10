using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Amanhencer.Tests;

public class AmanhencerPipelineTests
{
    [Test]
    public async Task When_ExecuteAsync_Should_CallAllMiddlewares()
    {
        var middlewares = Enumerable.Range(0, 3)
            .Select(_ =>
            {
                var m = Substitute.For<IMiddleware>();
                m
                    .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
                    .Returns(x =>
                    {
                        var context = (IPipelineContext)x[0];
                        var next = (Func<IPipelineContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();
        
        var pipeline = new AmanhencerPipeline(middlewares);

        var context = Substitute.For<IPipelineContext>();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        foreach (var middleware in middlewares)
        {
            await middleware
                .Received(1)
                .ExecuteAsync(context, Arg.Any<Func<IPipelineContext, ValueTask>>());
        }
    }
    
    [Test]
    public async Task When_ExecuteAsync_Should_CallNothingWhenCancellationRequested()
    {
        var middlewares = Enumerable.Range(0, 3)
            .Select(_ =>
            {
                var m = Substitute.For<IMiddleware>();
                m
                    .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
                    .Returns(x =>
                    {
                        var context = (IPipelineContext)x[0];
                        var next = (Func<IPipelineContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();
        
        var pipeline = new AmanhencerPipeline(middlewares);

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        
        var context = Substitute.For<IPipelineContext>();
        context.CancellationToken.Returns(cts.Token);

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        foreach (var middleware in middlewares)
        {
            await middleware
                .DidNotReceive()
                .ExecuteAsync(context, Arg.Any<Func<IPipelineContext, ValueTask>>());
        }
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_ExecuteAsync_Should_CallMiddlewaresInOrder()
    {
        var invocationOrder = new List<int>();
        var middlewares = Enumerable.Range(0, 3)
            .Select(index =>
            {
                var m = Substitute.For<IMiddleware>();
                m
                    .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
                    .Returns(x =>
                    {
                        invocationOrder.Add(index);
                        var context = (IPipelineContext)x[0];
                        var next = (Func<IPipelineContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();

        var pipeline = new AmanhencerPipeline(middlewares);

        var context = Substitute.For<IPipelineContext>();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        await Assert.That(invocationOrder)
            .IsEquivalentTo(new[] { 0, 1, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_StopWhenCancellationRequestedMidPipeline()
    {
        var cts = new CancellationTokenSource();

        var first = Substitute.For<IMiddleware>();
        first
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
            .Returns(x =>
            {
                cts.Cancel();
                var context = (IPipelineContext)x[0];
                var next = (Func<IPipelineContext, ValueTask>)x[1];
                return new ValueTask(next(context).AsTask());
            });

        var second = Substitute.For<IMiddleware>();
        var third = Substitute.For<IMiddleware>();

        var pipeline = new AmanhencerPipeline([first, second, third]);

        var context = Substitute.For<IPipelineContext>();
        context.CancellationToken.Returns(cts.Token);

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        await first
            .Received(1)
            .ExecuteAsync(context, Arg.Any<Func<IPipelineContext, ValueTask>>());

        await second
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>());

        await third
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_StopWhenMiddlewareDoesNotCallNext()
    {
        var first = Substitute.For<IMiddleware>();
        first
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
            .Returns(ValueTask.CompletedTask);

        var second = Substitute.For<IMiddleware>();
        var third = Substitute.For<IMiddleware>();

        var pipeline = new AmanhencerPipeline([first, second, third]);

        var context = Substitute.For<IPipelineContext>();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        await first
            .Received(1)
            .ExecuteAsync(context, Arg.Any<Func<IPipelineContext, ValueTask>>());

        await second
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>());

        await third
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_CompleteWhenPipelineIsEmpty()
    {
        var pipeline = new AmanhencerPipeline(ImmutableList<IMiddleware>.Empty);

        var context = Substitute.For<IPipelineContext>();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PropagateException()
    {
        var first = Substitute.For<IMiddleware>();
        first
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("boom"));

        var second = Substitute.For<IMiddleware>();

        var pipeline = new AmanhencerPipeline([first, second]);

        var context = Substitute.For<IPipelineContext>();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .Throws<InvalidOperationException>();

        await second
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>());
    }
}