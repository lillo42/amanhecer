using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class PipelineTests
{
    [Test]
    public async Task ExecuteAsync_RunsMiddlewaresInOrder()
    {
        var log = new List<string>();
        var pipeline = new AmanhencerPipeline([
            new OrderRecordingMiddleware("first", log),
            new OrderRecordingMiddleware("second", log)
        ]);

        await pipeline.ExecuteAsync(TestPipelineContext.Create());

        await Assert.That(string.Join(",", log)).IsEqualTo("first:before,second:before,second:after,first:after");
    }

    [Test]
    public async Task ExecuteAsync_EmptyPipeline_Completes()
    {
        var pipeline = new AmanhencerPipeline(ImmutableList<IMiddleware>.Empty);

        await Assert.That(async () => await pipeline.ExecuteAsync(TestPipelineContext.Create()))
            .ThrowsNothing();
    }

    [Test]
    public async Task ExecuteAsync_MiddlewareCanShortCircuitTheChain()
    {
        var log = new List<string>();
        var pipeline = new AmanhencerPipeline([
            new OrderRecordingMiddleware("first", log),
            new TerminalMiddleware(log),
            new OrderRecordingMiddleware("never", log)
        ]);

        await pipeline.ExecuteAsync(TestPipelineContext.Create());

        await Assert.That(string.Join(",", log)).IsEqualTo("first:before,terminal,first:after");
    }

    [Test]
    public async Task ExecuteAsync_CancellationRequested_SkipsMiddlewares()
    {
        var middleware = Substitute.For<IMiddleware>();
        var pipeline = new AmanhencerPipeline([middleware]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () =>
                await pipeline.ExecuteAsync(TestPipelineContext.Create(cancellationToken: cts.Token)))
            .ThrowsNothing();

        _ = middleware.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!);
    }

    [Test]
    public async Task ExecuteAsync_MiddlewareException_Propagates()
    {
        var middleware = Substitute.For<IMiddleware>();
        middleware.ExecuteAsync(Arg.Any<IPipelineContext>(), Arg.Any<Func<IPipelineContext, ValueTask>>())
            .Returns(_ => throw new InvalidOperationException("Middleware failed."));
        var pipeline = new AmanhencerPipeline([middleware]);

        await Assert.That(async () => await pipeline.ExecuteAsync(TestPipelineContext.Create()))
            .ThrowsExactly<InvalidOperationException>();
    }
}
