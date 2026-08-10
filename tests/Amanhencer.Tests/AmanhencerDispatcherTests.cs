using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerDispatcherTests
{
    private readonly IPipelineContextFactory _contextFactory;
    private readonly IPipelineFactory _pipelineFactory;
    private readonly AmanhencerDispatcher _dispatcher;

    public AmanhencerDispatcherTests()
    {
        _contextFactory = Substitute.For<IPipelineContextFactory>();
        _pipelineFactory = Substitute.For<IPipelineFactory>();
        _dispatcher =
            new AmanhencerDispatcher(_contextFactory, _pipelineFactory, new NullLogger<AmanhencerDispatcher>());
    }

    #region Send & SendAsync

    [Test]
    public async Task When_SendHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Send(request))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Send(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }


    [Test]
    public async Task When_SendAsyncHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_SendHasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(() => _dispatcher.Send(request))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Send(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_SendAsync_HasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_SendHasOnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Send(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Send(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }

    [Test]
    public async Task When_SendHasOnePipelineAndItIsTask_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        pipelineContext
            .ExecutingStrategy
            .ExecuteAsync(pipelineContext, pipelines)
            .Returns(new ValueTask(Delay()));

        await Assert.That(() => _dispatcher.Send(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Send(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        static async Task Delay()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    [Test]
    public async Task When_SendAsync_Has_OnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }

    #endregion

    #region Publish & PublishAsync

    [Test]
    public async Task When_PublishHasNoPipeline_Should_DoNothing()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }


    [Test]
    public async Task When_PublishAsyncHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.PublishAsync(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task When_PublishHasMultiPipeline_Should_Execute(int numberOfPipelines)
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = Enumerable.Range(0, numberOfPipelines)
            .Select(_ => Substitute.For<IPipeline>())
            .ToImmutableList();

        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext.ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext.ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }
    
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task When_PublishAsyncHasMultiPipeline_Should_Execute(int numberOfPipelines)
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = Enumerable.Range(0, numberOfPipelines)
            .Select(_ => Substitute.For<IPipeline>())
            .ToImmutableList();

        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.PublishAsync(request))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext.ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext.ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }

    #endregion
    
    #region Query & QueryAsync

    [Test]
    public async Task When_QueryHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }


    [Test]
    public async Task When_QueryAsyncHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_QueryHasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_QueryAsync_HasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);
    }

    [Test]
    public async Task When_QueryHasOnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);
        pipelineContext.Response.Returns(response);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }

    [Test]
    public async Task When_QueryHasOnePipelineAndItIsTask_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);
        pipelineContext.Response.Returns(response);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        pipelineContext
            .ExecutingStrategy
            .ExecuteAsync(pipelineContext, pipelines)
            .Returns(new ValueTask(Delay()));

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        static async Task Delay()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    [Test]
    public async Task When_QueryAsync_Has_OnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);
        pipelineContext.Response.Returns(response);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);

        var context = Substitute.For<IContext>();
        _contextFactory.Create(request, context, Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _contextFactory
            .Received()
            .Create(request, context, Arg.Any<CancellationToken>());

        _ = _pipelineFactory
            .Received()
            .Create(pipelineContext);

        await pipelineContext
            .ExecutingStrategy
            .Received()
            .ExecuteAsync(pipelineContext, pipelines);
    }

    #endregion

}