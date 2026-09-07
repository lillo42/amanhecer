using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Amanhecer.Tests;

public class AmanhecerPipelineTests
{
    [Test]
    public async Task When_ExecuteAsync_Should_CallAllMiddlewares()
    {
        var middlewares = Enumerable.Range(0, 3)
            .Select(_ =>
            {
                var m = Substitute.For<IMiddleware>();
                m
                    .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>())
                    .Returns(x =>
                    {
                        var context = (AmanhecerContext)x[0];
                        var next = (Func<AmanhecerContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();
        
        var pipeline = new AmanhecerPipeline(middlewares);

        var context = new AmanhecerContext();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        foreach (var middleware in middlewares)
        {
            await middleware
                .Received(1)
                .ExecuteAsync(context, Arg.Any<Func<AmanhecerContext, ValueTask>>());
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
                    .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>())
                    .Returns(x =>
                    {
                        var context = (AmanhecerContext)x[0];
                        var next = (Func<AmanhecerContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();
        
        var pipeline = new AmanhecerPipeline(middlewares);

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        
        var context = new AmanhecerContext { CancellationToken = cts.Token };

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .Throws<OperationCanceledException>();

        foreach (var middleware in middlewares)
        {
            await middleware
                .DidNotReceive()
                .ExecuteAsync(context, Arg.Any<Func<AmanhecerContext, ValueTask>>());
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
                    .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>())
                    .Returns(x =>
                    {
                        invocationOrder.Add(index);
                        var context = (AmanhecerContext)x[0];
                        var next = (Func<AmanhecerContext, ValueTask>)x[1];
                        return new ValueTask(next(context).AsTask());
                    });
                return m;
            })
            .ToImmutableList();

        var pipeline = new AmanhecerPipeline(middlewares);

        var context = new AmanhecerContext();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        await Assert.That(invocationOrder)
            .IsEquivalentTo([0, 1, 2], CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_StopWhenMiddlewareDoesNotCallNext()
    {
        var first = Substitute.For<IMiddleware>();
        first
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>())
            .Returns(ValueTask.CompletedTask);

        var second = Substitute.For<IMiddleware>();
        var third = Substitute.For<IMiddleware>();

        var pipeline = new AmanhecerPipeline([first, second, third]);

        var context = new AmanhecerContext();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();

        await first
            .Received(1)
            .ExecuteAsync(context, Arg.Any<Func<AmanhecerContext, ValueTask>>());

        await second
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>());

        await third
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_CompleteWhenPipelineIsEmpty()
    {
        var pipeline = new AmanhecerPipeline(ImmutableList<IMiddleware>.Empty);

        var context = new AmanhecerContext();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PropagateException()
    {
        var first = Substitute.For<IMiddleware>();
        first
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var second = Substitute.For<IMiddleware>();

        var pipeline = new AmanhecerPipeline([first, second]);

        var context = new AmanhecerContext();

        await Assert.That(async () => await pipeline.ExecuteAsync(context))
            .Throws<InvalidOperationException>();

        await second
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<Func<AmanhecerContext, ValueTask>>());
    }
}