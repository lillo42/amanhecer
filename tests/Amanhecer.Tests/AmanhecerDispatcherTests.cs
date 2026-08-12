using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Amanhecer.Tests;

public class AmanhecerDispatcherTests
{
    private readonly IPipelineContextFactory _contextFactory;
    private readonly IPipelineFactory _pipelineFactory;
    private readonly AmanhecerDispatcher _dispatcher;

    public AmanhecerDispatcherTests()
    {
        _contextFactory = Substitute.For<IPipelineContextFactory>();
        _pipelineFactory = Substitute.For<IPipelineFactory>();
        _dispatcher =
            new AmanhecerDispatcher(_contextFactory, _pipelineFactory, new NullLogger<AmanhecerDispatcher>());
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
    public async Task When_PublishAsyncHasNoPipeline_Should_DoNothing()
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

    [Test]
    public async Task When_PublishHasOnePipelineAndItIsTask_Should_ExecuteIt()
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

        static async Task Delay()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
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

    #region Null guards

    [Test]
    public async Task When_SendAsyncHasNullRequest_Should_Throw()
    {
        var context = Substitute.For<IContext>();

        await Assert.That(async () => await _dispatcher.SendAsync<string>(null!, context))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_SendAsyncHasNullContext_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        await Assert.That(async () => await _dispatcher.SendAsync(request, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_PublishAsyncHasNullRequest_Should_Throw()
    {
        var context = Substitute.For<IContext>();

        await Assert.That(async () => await _dispatcher.PublishAsync<string>(null!, context))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_PublishAsyncHasNullContext_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        await Assert.That(async () => await _dispatcher.PublishAsync(request, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_QueryAsyncHasNullQuery_Should_Throw()
    {
        var context = Substitute.For<IContext>();

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(null!, context))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_QueryAsyncHasNullContext_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Telemetry tags

    [Test]
    public async Task When_SendHasOnePipeline_Should_AddSendTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var tags = new List<KeyValuePair<string, object?>>();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns(tags);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Send(request))
            .ThrowsNothing();

        await Assert.That(tags)
            .Contains(tag => tag is { Key: "amanhecer.operation", Value: "send" });
    }

    [Test]
    public async Task When_PublishHasOnePipeline_Should_AddPostTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var tags = new List<KeyValuePair<string, object?>>();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns(tags);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        await Assert.That(tags)
            .Contains(tag => tag is { Key: "amanhecer.operation", Value: "publish" });
    }

    [Test]
    public async Task When_PublishHasNoPipeline_Should_NotAddTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var tags = new List<KeyValuePair<string, object?>>();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns(tags);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        _pipelineFactory.Create(pipelineContext)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        await Assert.That(tags).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_QueryHasOnePipeline_Should_AddQueryTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();
        var tags = new List<KeyValuePair<string, object?>>();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns(tags);
        pipelineContext.Response.Returns(response);

        _contextFactory.Create(request, Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        await Assert.That(tags)
            .Contains(tag => tag is { Key: "amanhecer.operation", Value: "query" });
    }

    #endregion

    #region CancellationToken forwarding

    [Test]
    public async Task When_SendAsync_Should_ForwardCancellationToken()
    {
        var request = Guid.NewGuid().ToString();
        var context = Substitute.For<IContext>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, context, cancellationTokenSource.Token)
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await _dispatcher.SendAsync(request, context, cancellationTokenSource.Token);

        _ = _contextFactory
            .Received()
            .Create(request, context, cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_PublishAsync_Should_ForwardCancellationToken()
    {
        var request = Guid.NewGuid().ToString();
        var context = Substitute.For<IContext>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);

        _contextFactory.Create(request, context, cancellationTokenSource.Token)
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await _dispatcher.PublishAsync(request, context, cancellationTokenSource.Token);

        _ = _contextFactory
            .Received()
            .Create(request, context, cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_QueryAsync_Should_ForwardCancellationToken()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();
        var context = Substitute.For<IContext>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelineContext = Substitute.For<IPipelineContext>();
        pipelineContext.TelemetryTags.Returns([]);
        pipelineContext.Response.Returns(response);

        _contextFactory.Create(request, context, cancellationTokenSource.Token)
            .Returns(pipelineContext);

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(pipelineContext)
            .Returns(pipelines);

        await _dispatcher.QueryAsync<string, object?>(request, context, cancellationTokenSource.Token);

        _ = _contextFactory
            .Received()
            .Create(request, context, cancellationTokenSource.Token);
    }

    #endregion
}