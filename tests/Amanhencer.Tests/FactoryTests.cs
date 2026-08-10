using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.ExecutingStrategies;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhencer.Tests;

public class FactoryTests
{
    private static AmanhencerPipelineOptions CreateOptions(string routingKey, params AmanhencerMiddlewareOptions[] middlewares)
    {
        var configuration = new Dictionary<string, ImmutableList<ImmutableList<AmanhencerMiddlewareOptions>>>
        {
            [routingKey] = [middlewares.ToImmutableList()]
        };
        return new AmanhencerPipelineOptions(configuration.ToFrozenDictionary());
    }

    [Test]
    public async Task ContextFactory_UsesExplicitRoutingKeyFromContext()
    {
        var factory = new AmanhencerPipelineContextFactory(new SequenceExecutingStrategy());
        var context = new AmanhencerContext { RoutingKey = "explicit.key" };

        var pipelineContext = factory.Create(new TestRequest("hello"), context, CancellationToken.None);

        await Assert.That(pipelineContext.RoutingKey).IsEqualTo("explicit.key");
    }

    [Test]
    public async Task ContextFactory_UsesRoutingKeyAttribute()
    {
        var factory = new AmanhencerPipelineContextFactory(new SequenceExecutingStrategy());

        var pipelineContext = factory.Create(new RoutedRequest("hello"), new AmanhencerContext(), CancellationToken.None);

        await Assert.That(pipelineContext.RoutingKey).IsEqualTo("routed.request");
    }

    [Test]
    public async Task ContextFactory_FallsBackToRequestTypeFullName()
    {
        var factory = new AmanhencerPipelineContextFactory(new SequenceExecutingStrategy());

        var pipelineContext = factory.Create(new TestRequest("hello"), new AmanhencerContext(), CancellationToken.None);

        await Assert.That(pipelineContext.RoutingKey).IsEqualTo(typeof(TestRequest).FullName!);
    }

    [Test]
    public async Task ContextFactory_UsesContextExecutingStrategyWhenSet()
    {
        var factory = new AmanhencerPipelineContextFactory(new SequenceExecutingStrategy());
        var strategy = new ParallelExecutingStrategy(new ParallelOptions());
        var context = new AmanhencerContext { ExecutingStrategy = strategy };

        var pipelineContext = factory.Create(new TestRequest("hello"), context, CancellationToken.None);

        await Assert.That(ReferenceEquals(strategy, pipelineContext.ExecutingStrategy)).IsTrue();
    }

    [Test]
    public async Task ContextFactory_UsesDefaultStrategyWhenContextHasNone()
    {
        var defaultStrategy = new SequenceExecutingStrategy();
        var factory = new AmanhencerPipelineContextFactory(defaultStrategy);

        var pipelineContext = factory.Create(new TestRequest("hello"), new AmanhencerContext(), CancellationToken.None);

        await Assert.That(ReferenceEquals(defaultStrategy, pipelineContext.ExecutingStrategy)).IsTrue();
    }

    [Test]
    public async Task ContextFactory_CopiesRequestMetadataAndCancellationToken()
    {
        var factory = new AmanhencerPipelineContextFactory(new SequenceExecutingStrategy());
        var request = new TestRequest("hello");
        var context = new AmanhencerContext();
        context.Metadata["key"] = "value";
        using var cts = new CancellationTokenSource();

        var pipelineContext = factory.Create(request, context, cts.Token);

        await Assert.That(ReferenceEquals(request, pipelineContext.Request)).IsTrue();
        await Assert.That(ReferenceEquals(context.Metadata, pipelineContext.Metadata)).IsTrue();
        await Assert.That(pipelineContext.CancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task PipelineFactory_ReturnsEmpty_WhenRoutingKeyIsNotConfigured()
    {
        var middlewareFactory = Substitute.For<IMiddlewareFactory>();
        var factory = new AmanhencerPipelineFactory(
            new AmanhencerPipelineOptions(FrozenDictionary<string, ImmutableList<ImmutableList<AmanhencerMiddlewareOptions>>>.Empty),
            middlewareFactory);

        var pipelines = factory.Create(TestPipelineContext.Create(routingKey: "unknown"));

        await Assert.That(pipelines).IsEmpty();
        middlewareFactory.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Test]
    public async Task PipelineFactory_CreatesMiddlewaresThroughFactory()
    {
        var middlewareFactory = Substitute.For<IMiddlewareFactory>();
        middlewareFactory.Create(Arg.Any<Type>(), Arg.Any<object?>()).Returns(new PassThroughMiddleware());
        var options = CreateOptions("test.key",
            new AmanhencerMiddlewareOptions(typeof(PassThroughMiddleware), 0, "one"),
            new AmanhencerMiddlewareOptions(typeof(MetadataMiddleware), 1, "two"));
        var factory = new AmanhencerPipelineFactory(options, middlewareFactory);

        var pipelines = factory.Create(TestPipelineContext.Create(routingKey: "test.key"));

        await Assert.That(pipelines.Count).IsEqualTo(1);
        middlewareFactory.Received(1).Create(typeof(PassThroughMiddleware), "one");
        middlewareFactory.Received(1).Create(typeof(MetadataMiddleware), "two");
    }

    [Test]
    public async Task PipelineFactory_BuildsExecutablePipeline()
    {
        var log = new List<string>();
        var middlewareFactory = Substitute.For<IMiddlewareFactory>();
        middlewareFactory.Create(Arg.Any<Type>(), Arg.Any<object?>())
            .Returns(new OrderRecordingMiddleware("recorded", log));
        var options = CreateOptions("test.key",
            new AmanhencerMiddlewareOptions(typeof(OrderRecordingMiddleware), 0, null));
        var factory = new AmanhencerPipelineFactory(options, middlewareFactory);

        var pipelines = factory.Create(TestPipelineContext.Create(routingKey: "test.key"));
        await pipelines.Single().ExecuteAsync(TestPipelineContext.Create());

        await Assert.That(string.Join(",", log)).IsEqualTo("recorded:before,recorded:after");
    }

    [Test]
    public async Task HandlerFactory_ResolvesHandlerFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionLog());
        services.AddTransient<TestRequestHandler>();
        var provider = services.BuildServiceProvider();
        var factory = new AmanhencerHandlerFactory(provider);

        var handler = factory.Create(typeof(TestRequestHandler), TestPipelineContext.Create());

        await Assert.That(handler is TestRequestHandler).IsTrue();
    }

    [Test]
    public async Task HandlerFactory_Throws_WhenHandlerIsNotRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new AmanhencerHandlerFactory(provider);

        await Assert.That(() => factory.Create(typeof(TestRequestHandler), TestPipelineContext.Create()))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task MiddlewareFactory_ResolvesAndInitializesMiddleware()
    {
        var services = new ServiceCollection();
        services.AddTransient<MetadataMiddleware>();
        var provider = services.BuildServiceProvider();
        var factory = new AmanhencerMiddlewareFactory(provider);

        var middleware = factory.Create(typeof(MetadataMiddleware), "metadata");

        await Assert.That(middleware is MetadataMiddleware).IsTrue();
        await Assert.That(((MetadataMiddleware)middleware).ReceivedMetadata).IsEqualTo("metadata");
    }

    [Test]
    public async Task MiddlewareFactory_Throws_WhenMiddlewareIsNotRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new AmanhencerMiddlewareFactory(provider);

        await Assert.That(() => factory.Create(typeof(MetadataMiddleware), null))
            .ThrowsExactly<InvalidOperationException>();
    }
}
