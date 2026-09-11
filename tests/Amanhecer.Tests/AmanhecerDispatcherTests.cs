using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Messaging.Middlewares;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Amanhecer.Tests;

public class AmanhecerDispatcherTests
{
    private readonly IPipelineFactory _pipelineFactory;
    private readonly IExecutingStrategy _defaultStrategy;
    private readonly AmanhecerDispatcher _dispatcher;

    public AmanhecerDispatcherTests()
    {
        _pipelineFactory = Substitute.For<IPipelineFactory>();
        _defaultStrategy = Substitute.For<IExecutingStrategy>();
        _dispatcher = new AmanhecerDispatcher(_pipelineFactory, _defaultStrategy,
            new NullLogger<AmanhecerDispatcher>());
    }

    #region Send & SendAsync

    [Test]
    public async Task When_SendHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Send(request))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Send(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_SendAsyncHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_SendHasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(() => _dispatcher.Send(request))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Send(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_SendAsync_HasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_SendHasOnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Send(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Send(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    [Test]
    public async Task When_SendHasOnePipelineAndItIsTask_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        _defaultStrategy
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines)
            .Returns(new ValueTask(Delay()));

        await Assert.That(() => _dispatcher.Send(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Send(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);

        static async Task Delay()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    [Test]
    public async Task When_SendAsync_Has_OnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.SendAsync(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.SendAsync(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    #endregion

    #region Publish & PublishAsync

    [Test]
    public async Task When_PublishHasNoPipeline_Should_DoNothing()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_PublishAsyncHasNoPipeline_Should_DoNothing()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.PublishAsync(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task When_PublishHasMultiPipeline_Should_Execute(int numberOfPipelines)
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = Enumerable.Range(0, numberOfPipelines)
            .Select(_ => Substitute.For<IPipeline>())
            .ToImmutableList();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task When_PublishAsyncHasMultiPipeline_Should_Execute(int numberOfPipelines)
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = Enumerable.Range(0, numberOfPipelines)
            .Select(_ => Substitute.For<IPipeline>())
            .ToImmutableList();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.PublishAsync(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    [Test]
    public async Task When_PublishHasOnePipelineAndItIsTask_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        _defaultStrategy
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines)
            .Returns(new ValueTask(Delay()));

        await Assert.That(() => _dispatcher.Publish(request))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);

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

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_QueryAsyncHasNoPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .Throws<PipelineNotFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_QueryHasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_QueryAsync_HasMultiPipeline_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns([Substitute.For<IPipeline>(), Substitute.For<IPipeline>()]);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .Throws<MultiPipelineFoundException>();

        _ = _pipelineFactory
            .Received()
            .Create(context);
    }

    [Test]
    public async Task When_QueryHasOnePipeline_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        _defaultStrategy
            .When(x => x.ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines))
            .Do(call => call.Arg<AmanhecerContext>().Response = response);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    [Test]
    public async Task When_QueryHasOnePipelineAndItIsTask_Should_ExecuteIt()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        _defaultStrategy
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines)
            .Returns(new ValueTask(Delay()));

        _defaultStrategy
            .When(x => x.ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines))
            .Do(call => call.Arg<AmanhecerContext>().Response = response);

        await Assert.That(() => _dispatcher.Query<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);

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

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        _defaultStrategy
            .When(x => x.ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines))
            .Do(call => call.Arg<AmanhecerContext>().Response = response);

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(Arg.Any<AmanhecerContext>());

        await _defaultStrategy
            .Received()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), pipelines);

        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.QueryAsync<string, object?>(request, context))
            .ThrowsNothing()
            .And.IsEqualTo(response);

        _ = _pipelineFactory
            .Received()
            .Create(context);

        await _defaultStrategy
            .Received()
            .ExecuteAsync(context, pipelines);
    }

    #endregion

    #region Post & PostAsync

    [Test]
    public async Task When_PostAsyncIsCalledTwiceWithTheSameContext_Should_LeaveTheContextUntouched()
    {
        var request = new SomeRequest();
        var context = new AmanhecerContext();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.PostAsync(request, context))
            .ThrowsNothing();

        await Assert.That(async () => await _dispatcher.PostAsync(request, context))
            .ThrowsNothing();

        await Assert.That(context.RoutingKey).IsEqualTo("");
        await Assert.That(context.Middlewares).IsNull();
        await Assert.That(context.Metadata).IsEmpty();

        _ = _pipelineFactory
            .Received(2)
            .Create(Arg.Is<AmanhecerContext>(c =>
                !ReferenceEquals(c, context) &&
                c.RoutingKey == "Amanhecer.Messaging.Post" &&
                (string?)c.Metadata[MetadataName.PublicationRoutingKey] ==
                    "Amanhecer.Tests.AmanhecerDispatcherTests+SomeRequest" &&
                c.Middlewares!.Count == 1 &&
                c.Middlewares[0].MiddlewareType == typeof(EncodeMiddleware)));
    }

    [Test]
    public async Task When_PostAsyncHasNullMessage_Should_Throw()
    {
        var context = new AmanhecerContext();

        await Assert.That(async () => await _dispatcher.PostAsync<string>(null!, context))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task When_PostAsyncHasNullContext_Should_Throw()
    {
        var request = Guid.NewGuid().ToString();

        await Assert.That(async () => await _dispatcher.PostAsync(request, null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Context preparation

    [Test]
    [Arguments(typeof(SomeRequest), null, "Amanhecer.Tests.AmanhecerDispatcherTests+SomeRequest")]
    [Arguments(typeof(SomeRequest), "random-name", "random-name")]
    [Arguments(typeof(SomeRequestWithAttribute), "random-name", "random-name")]
    [Arguments(typeof(SomeRequestWithAttribute), null, "some-request")]
    public async Task When_Dispatch_Should_ResolveTheRoutingKeyCorrectly(
        Type type, string? routingKey, string expectedRoutingKey)
    {
        var request = Activator.CreateInstance(type)!;
        var context = new AmanhecerContext { RoutingKey = routingKey ?? "" };

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        await Assert.That(context.RoutingKey).IsEqualTo(expectedRoutingKey);
    }

    [Test]
    public async Task When_Dispatch_Should_DefaultTheExecutingStrategy()
    {
        var request = Guid.NewGuid().ToString();
        var context = new AmanhecerContext();

        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        await Assert.That(context.ExecutingStrategy).IsSameReferenceAs(_defaultStrategy);
    }

    [Test]
    public async Task When_Dispatch_Should_KeepTheContextExecutingStrategy()
    {
        var request = Guid.NewGuid().ToString();
        var executingStrategy = Substitute.For<IExecutingStrategy>();
        var context = new AmanhecerContext { ExecutingStrategy = executingStrategy };

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(Arg.Any<AmanhecerContext>())
            .Returns(pipelines);

        await Assert.That(async () => await _dispatcher.PublishAsync(request, context))
            .ThrowsNothing();

        await Assert.That(context.ExecutingStrategy).IsSameReferenceAs(executingStrategy);

        await executingStrategy
            .Received()
            .ExecuteAsync(context, pipelines);

        await _defaultStrategy
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<AmanhecerContext>(), Arg.Any<IReadOnlyList<IPipeline>>());
    }

    #endregion

    #region Null guards

    [Test]
    public async Task When_SendAsyncHasNullRequest_Should_Throw()
    {
        var context = new AmanhecerContext();

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
        var context = new AmanhecerContext();

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
        var context = new AmanhecerContext();

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
        var context = new AmanhecerContext { TelemetryTags = tags };

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Send(request, context))
            .ThrowsNothing();

        await Assert.That(tags)
            .Contains(tag => tag is { Key: "amanhecer.operation", Value: "send" });
    }

    [Test]
    public async Task When_PublishHasOnePipeline_Should_AddPostTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var tags = new List<KeyValuePair<string, object?>>();
        var context = new AmanhecerContext { TelemetryTags = tags };

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        await Assert.That(tags)
            .Contains(tag => tag is { Key: "amanhecer.operation", Value: "publish" });
    }

    [Test]
    public async Task When_PublishHasNoPipeline_Should_NotAddTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var tags = new List<KeyValuePair<string, object?>>();
        var context = new AmanhecerContext { TelemetryTags = tags };

        _pipelineFactory.Create(context)
            .Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(() => _dispatcher.Publish(request, context))
            .ThrowsNothing();

        await Assert.That(tags).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_QueryHasOnePipeline_Should_AddQueryTelemetryTag()
    {
        var request = Guid.NewGuid().ToString();
        var response = new object();
        var tags = new List<KeyValuePair<string, object?>>();
        var context = new AmanhecerContext { TelemetryTags = tags };

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        _defaultStrategy
            .When(x => x.ExecuteAsync(context, pipelines))
            .Do(call => call.Arg<AmanhecerContext>().Response = response);

        await Assert.That(() => _dispatcher.Query<string, object?>(request, context))
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
        var context = new AmanhecerContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        await _dispatcher.SendAsync(request, context, cancellationTokenSource.Token);

        await Assert.That(context.CancellationToken).IsEqualTo(cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_PublishAsync_Should_ForwardCancellationToken()
    {
        var request = Guid.NewGuid().ToString();
        var context = new AmanhecerContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        await _dispatcher.PublishAsync(request, context, cancellationTokenSource.Token);

        await Assert.That(context.CancellationToken).IsEqualTo(cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_QueryAsync_Should_ForwardCancellationToken()
    {
        var request = Guid.NewGuid().ToString();
        var context = new AmanhecerContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        var pipelines = ImmutableList.Create(Substitute.For<IPipeline>());
        _pipelineFactory.Create(context)
            .Returns(pipelines);

        await _dispatcher.QueryAsync<string, object?>(request, context, cancellationTokenSource.Token);

        await Assert.That(context.CancellationToken).IsEqualTo(cancellationTokenSource.Token);
    }

    #endregion

    public record SomeRequest;

    [RoutingKey("some-request")]
    public record SomeRequestWithAttribute;
}
