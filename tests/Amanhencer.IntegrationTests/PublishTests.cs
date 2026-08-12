using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.IntegrationTests;

public class PublishTests : BaseTests
{
    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<ExecutedRequests>();
    }

    protected override void ConfigureAmanhencer(AmanhencerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<OneSyncRequestHandler>()
            .AddRequestHandler<OneAsyncRequestHandler>()
            .AddRequestHandler<FirstMultiRequestHandler>()
            .AddRequestHandler<SecondMultiRequestHandler>()
            .AddRequestHandler<OkMixedRequestHandler>()
            .AddRequestHandler<FailingMixedRequestHandler>();
    }

    [Test]
    public async Task When_Publish_AndNoHandlerRegistered_Should_DoNothing()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(() => dispatcher.Publish(new NoRequestHandler()))
            .ThrowsNothing();

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_PublishAsync_AndNoHandlerRegistered_Should_DoNothing()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.PublishAsync(new NoRequestHandler()))
            .ThrowsNothing();

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_Publish_Should_ExecuteHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        dispatcher.Publish(new OneSyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneSyncRequest));
    }

    [Test]
    public async Task When_PublishAsync_Should_ExecuteHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.PublishAsync(new OneAsyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneAsyncRequest));
    }

    [Test]
    public async Task When_Publish_Should_ExecuteAsyncHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        dispatcher.Publish(new OneAsyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneAsyncRequest));
    }

    [Test]
    public async Task When_Publish_AndMultipleHandlersRegistered_Should_ExecuteAllHandlers()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        dispatcher.Publish(new MultiRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests)
            .Contains(nameof(FirstMultiRequestHandler))
            .And.Contains(nameof(SecondMultiRequestHandler));
    }

    [Test]
    public async Task When_PublishAsync_AndMultipleHandlersRegistered_Should_ExecuteAllHandlers()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.PublishAsync(new MultiRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests)
            .Contains(nameof(FirstMultiRequestHandler))
            .And.Contains(nameof(SecondMultiRequestHandler));
    }

    [Test]
    public async Task When_PublishAsync_AndOneHandlerFails_Should_ExecuteRemainingHandlersAndThrowAggregateException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.PublishAsync(new MixedRequest()))
            .Throws<AggregateException>()
            .And.Member(x => x.InnerExceptions.Count, y => y.IsEqualTo(1));

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OkMixedRequestHandler));
    }

    private class ExecutedRequests
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyCollection<string> Requests => [.. _requests];

        public void Add(string request) => _requests.Enqueue(request);
    }

    private record NoRequestHandler;

    private record OneSyncRequest;

    private class OneSyncRequestHandler(ExecutedRequests executed) : RequestHandler<OneSyncRequest>
    {
        public override ValueTask HandleAsync(OneSyncRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            executed.Add(nameof(OneSyncRequest));
            return new ValueTask();
        }
    }

    private record OneAsyncRequest;

    private class OneAsyncRequestHandler(ExecutedRequests executed) : RequestHandler<OneAsyncRequest>
    {
        public override async ValueTask HandleAsync(OneAsyncRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            executed.Add(nameof(OneAsyncRequest));
        }
    }

    private record MultiRequest;

    private class FirstMultiRequestHandler(ExecutedRequests executed) : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            executed.Add(nameof(FirstMultiRequestHandler));
            return new ValueTask();
        }
    }

    private class SecondMultiRequestHandler(ExecutedRequests executed) : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            executed.Add(nameof(SecondMultiRequestHandler));
            return new ValueTask();
        }
    }

    private record MixedRequest;

    private class OkMixedRequestHandler(ExecutedRequests executed) : RequestHandler<MixedRequest>
    {
        public override ValueTask HandleAsync(MixedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            executed.Add(nameof(OkMixedRequestHandler));
            return new ValueTask();
        }
    }

    private class FailingMixedRequestHandler : RequestHandler<MixedRequest>
    {
        public override ValueTask HandleAsync(MixedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
