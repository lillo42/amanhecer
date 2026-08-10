using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;

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
}