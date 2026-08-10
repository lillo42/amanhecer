using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;
using Amanhencer.Configurator;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhencer.Tests;

public class DispatcherTests
{
    private static (IDispatcher Dispatcher, ExecutionLog Log) CreateDispatcher(Action<AmanhencerConfigurator> configure)
    {
        var log = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddAmanhencer(configure);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IDispatcher>(), log);
    }

    private static (AmanhencerDispatcher Dispatcher, IPipelineContextFactory ContextFactory, IPipelineFactory PipelineFactory)
        CreateSubstituteDispatcher()
    {
        var contextFactory = Substitute.For<IPipelineContextFactory>();
        contextFactory.Create(Arg.Any<object>(), Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => TestPipelineContext.Create());
        var pipelineFactory = Substitute.For<IPipelineFactory>();
        return (new AmanhencerDispatcher(contextFactory, pipelineFactory), contextFactory, pipelineFactory);
    }

    private static string KeyOf<T>() => typeof(T).FullName!;

    [Test]
    public async Task SendAsync_ExecutesRegisteredHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<TestRequestHandler>());

        await dispatcher.SendAsync(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("handled:hello");
    }

    [Test]
    public async Task Send_ExecutesRegisteredHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<TestRequestHandler>());

        dispatcher.Send(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("handled:hello");
    }

    [Test]
    public async Task Send_WithContext_ExecutesRegisteredHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<TestRequestHandler>());

        dispatcher.Send(new TestRequest("hello"), new AmanhencerContext());

        await Assert.That(log.Entries).Contains("handled:hello");
    }

    [Test]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.SendAsync<TestRequest>(null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task SendAsync_NullContext_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.SendAsync(new TestRequest("hello"), (IContext)null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task SendAsync_NoPipeline_ThrowsPipelineNotFoundException()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await dispatcher.SendAsync(new TestRequest("hello")))
            .ThrowsExactly<PipelineNotFoundException>();
    }

    [Test]
    public async Task SendAsync_MultiplePipelines_ThrowsMultiPipelineFoundException()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        pipelineFactory.Create(Arg.Any<IPipelineContext>())
            .Returns(ImmutableList.Create(Substitute.For<IPipeline>(), Substitute.For<IPipeline>()));

        await Assert.That(async () => await dispatcher.SendAsync(new TestRequest("hello")))
            .ThrowsExactly<MultiPipelineFoundException>();
    }

    [Test]
    public async Task SendAsync_UsesRoutingKeyAttribute()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<RoutedRequestHandler>());

        await dispatcher.SendAsync(new RoutedRequest("hello"));

        await Assert.That(log.Entries).Contains("routed:hello");
    }

    [Test]
    public async Task SendAsync_ExplicitRoutingKey_OverridesAttribute()
    {
        var (dispatcher, log) = CreateDispatcher(cfg =>
            cfg.AddRoutingKey("custom.key", c => c.UseHandler<RoutedRequestHandler>()));

        await dispatcher.SendAsync(new RoutedRequest("hello"), new AmanhencerContext { RoutingKey = "custom.key" });

        await Assert.That(log.Entries).Contains("routed:hello");
    }

    [Test]
    public async Task Send_PropagatesHandlerException()
    {
        var (dispatcher, _) = CreateDispatcher(cfg => cfg.AddRequestHandler<FailingRequestHandler>());

        await Assert.That(() => dispatcher.Send(new TestRequest("hello")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_RunsMiddlewaresInOrderAroundHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<TestRequestHandler>(routing =>
            routing.Use<FirstMiddleware>(order: 1).Use<SecondMiddleware>(order: 2)));

        await dispatcher.SendAsync(new TestRequest("hello"));

        await Assert.That(log.Joined())
            .IsEqualTo("first:before,second:before,handled:hello,second:after,first:after");
    }

    [Test]
    public async Task SendAsync_RunsMiddlewaresDeclaredByAttributes()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<AttributedRequestHandler>());

        await dispatcher.SendAsync(new AttributedRequest("hello"));

        await Assert.That(log.Joined())
            .IsEqualTo("first:before,second:before,handled:hello,second:after,first:after");
    }

    [Test]
    public async Task PublishAsync_NoPipelines_Completes()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await dispatcher.PublishAsync(new TestRequest("hello")))
            .ThrowsNothing();
    }

    [Test]
    public async Task PublishAsync_MultiplePipelines_ExecutesAll()
    {
        var (dispatcher, log) = CreateDispatcher(cfg =>
        {
            cfg.AddRoutingKey(KeyOf<TestRequest>(), c => c.UseHandler<TestRequestHandler>());
            cfg.AddRoutingKey(KeyOf<TestRequest>(), c => c.UseHandler<SecondTestRequestHandler>());
        });

        await dispatcher.PublishAsync(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("handled:hello");
        await Assert.That(log.Entries).Contains("second:hello");
    }

    [Test]
    public async Task Publish_MultiplePipelines_ExecutesAll()
    {
        var (dispatcher, log) = CreateDispatcher(cfg =>
        {
            cfg.AddRoutingKey(KeyOf<TestRequest>(), c => c.UseHandler<TestRequestHandler>());
            cfg.AddRoutingKey(KeyOf<TestRequest>(), c => c.UseHandler<SecondTestRequestHandler>());
        });

        dispatcher.Publish(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("handled:hello");
        await Assert.That(log.Entries).Contains("second:hello");
    }

    [Test]
    public async Task PublishAsync_NullRequest_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.PublishAsync<TestRequest>(null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task PublishAsync_NullContext_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.PublishAsync(new TestRequest("hello"), (IContext)null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task QueryAsync_ReturnsHandlerResponse()
    {
        var (dispatcher, _) = CreateDispatcher(cfg => cfg.AddQueryHandler<TestQueryHandler>());

        var response = await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42));

        await Assert.That(response).IsEqualTo("answer:42");
    }

    [Test]
    public async Task Query_ReturnsHandlerResponse()
    {
        var (dispatcher, _) = CreateDispatcher(cfg => cfg.AddQueryHandler<TestQueryHandler>());

        var response = dispatcher.Query<TestQuery, string>(new TestQuery(42));

        await Assert.That(response).IsEqualTo("answer:42");
    }

    [Test]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.QueryAsync<TestQuery, string>(null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task QueryAsync_NullContext_ThrowsArgumentNullException()
    {
        var (dispatcher, contextFactory, pipelineFactory) = CreateSubstituteDispatcher();

        await Assert.That(async () => await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42), (IContext)null!))
            .ThrowsExactly<ArgumentNullException>();

        contextFactory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public async Task QueryAsync_NoPipeline_ThrowsPipelineNotFoundException()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList<IPipeline>.Empty);

        await Assert.That(async () => await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42)))
            .ThrowsExactly<PipelineNotFoundException>();
    }

    [Test]
    public async Task QueryAsync_MultiplePipelines_ThrowsMultiPipelineFoundException()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        pipelineFactory.Create(Arg.Any<IPipelineContext>())
            .Returns(ImmutableList.Create(Substitute.For<IPipeline>(), Substitute.For<IPipeline>()));

        await Assert.That(async () => await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42)))
            .ThrowsExactly<MultiPipelineFoundException>();
    }

    [Test]
    public async Task SendAsync_AddsOperationTelemetryTag()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        IPipelineContext? captured = null;
        var pipeline = Substitute.For<IPipeline>();
        pipeline.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
            .Do(ci => captured = ci.Arg<IPipelineContext>());
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList.Create(pipeline));

        await dispatcher.SendAsync(new TestRequest("hello"));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.TelemetryTags.Single(t => t.Key == "amanhencer.operation").Value)
            .IsEqualTo("send");
    }

    [Test]
    public async Task PublishAsync_AddsOperationTelemetryTag()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        IPipelineContext? captured = null;
        var pipeline = Substitute.For<IPipeline>();
        pipeline.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
            .Do(ci => captured = ci.Arg<IPipelineContext>());
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList.Create(pipeline));

        await dispatcher.PublishAsync(new TestRequest("hello"));

        await Assert.That(captured).IsNotNull();
        // The publish operation is currently tagged "post".
        await Assert.That(captured!.TelemetryTags.Single(t => t.Key == "amanhencer.operation").Value)
            .IsEqualTo("post");
    }

    [Test]
    public async Task QueryAsync_AddsOperationTelemetryTag()
    {
        var (dispatcher, _, pipelineFactory) = CreateSubstituteDispatcher();
        IPipelineContext? captured = null;
        var pipeline = Substitute.For<IPipeline>();
        pipeline.When(p => p.ExecuteAsync(Arg.Any<IPipelineContext>()))
            .Do(ci => captured = ci.Arg<IPipelineContext>());
        pipelineFactory.Create(Arg.Any<IPipelineContext>()).Returns(ImmutableList.Create(pipeline));

        await dispatcher.QueryAsync<TestQuery, string>(new TestQuery(42));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.TelemetryTags.Single(t => t.Key == "amanhencer.operation").Value)
            .IsEqualTo("query");
    }

    [Test]
    public async Task QueryAsync_HandlerProducesNoResponse_ReturnsNull()
    {
        var (dispatcher, _) = CreateDispatcher(cfg => cfg.AddQueryHandler<NullResponseQueryHandler>());

        var response = await dispatcher.QueryAsync<TestQuery, string?>(new TestQuery(42));

        // The dispatcher casts the pipeline context's response without validating it,
        // so a handler that produces no response yields null rather than throwing.
        await Assert.That(response).IsNull();
    }

    [Test]
    public async Task SendAsync_AttributeMiddleware_ReceivesAttributeInstanceAsMetadata()
    {
        var capture = new MetadataCapture();
        var services = new ServiceCollection();
        services.AddSingleton(capture);
        services.AddAmanhencer(cfg => cfg.AddRequestHandler<MetadataAttributedRequestHandler>());
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new MetadataAttributedRequest("hello"));

        var attribute = capture.Metadata as MetadataCaptureMiddlewareAttribute;
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.Order).IsEqualTo(3);
    }

    [Test]
    public async Task Send_GenuinelyAsyncHandler_ExecutesRegisteredHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<AsyncTestRequestHandler>());

        dispatcher.Send(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("async:hello");
    }

    [Test]
    public async Task Publish_GenuinelyAsyncHandler_ExecutesRegisteredHandler()
    {
        var (dispatcher, log) = CreateDispatcher(cfg =>
            cfg.AddRoutingKey(KeyOf<TestRequest>(), c => c.UseHandler<AsyncTestRequestHandler>()));

        dispatcher.Publish(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("async:hello");
    }

    [Test]
    public async Task Query_GenuinelyAsyncHandler_ReturnsHandlerResponse()
    {
        var (dispatcher, _) = CreateDispatcher(cfg => cfg.AddQueryHandler<AsyncTestQueryHandler>());

        var response = dispatcher.Query<TestQuery, string>(new TestQuery(42));

        await Assert.That(response).IsEqualTo("async-answer:42");
    }

    [Test]
    public async Task Send_ValueTaskSourceBackedHandler_ExecutesRegisteredHandler()
    {
        // Locks in the sync-over-async wrappers consuming the returned ValueTask exactly once:
        // double consumption is undefined behavior for non-Task-backed ValueTasks.
        var (dispatcher, log) = CreateDispatcher(cfg => cfg.AddRequestHandler<ValueTaskSourceRequestHandler>());

        dispatcher.Send(new TestRequest("hello"));

        await Assert.That(log.Entries).Contains("valuetask-source:hello");
    }
}
