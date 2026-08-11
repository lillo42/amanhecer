using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;
using Amanhencer.Configurator;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.IntegrationTests;

public class SendTests : BaseTests
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
            .AddRequestHandler<SecondMultiRequestHandler>();
    }

    [Test]
    public async Task When_Send_AndNoHandlerRegistered_Should_ThrowPipelineNotFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(() => dispatcher.Send(new NoRequestHandler()))
            .Throws<PipelineNotFoundException>()
            .WithMessageContaining(typeof(NoRequestHandler).FullName!);
    }

    [Test]
    public async Task When_SendAsync_AndNoHandlerRegistered_Should_ThrowPipelineNotFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.SendAsync(new NoRequestHandler()))
            .Throws<PipelineNotFoundException>()
            .WithMessageContaining(typeof(NoRequestHandler).FullName!);
    }

    [Test]
    public async Task When_Send_Should_ExecuteHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        dispatcher.Send(new OneSyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneSyncRequest));
    }

    [Test]
    public async Task When_SendAsync_Should_ExecuteHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new OneAsyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneAsyncRequest));
    }

    [Test]
    public async Task When_Send_Should_ExecuteAsyncHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        dispatcher.Send(new OneAsyncRequest());

        var executed = ServiceProvider.GetRequiredService<ExecutedRequests>();
        await Assert.That(executed.Requests).Contains(nameof(OneAsyncRequest));
    }

    [Test]
    public async Task When_Send_AndMultipleHandlersRegistered_Should_ThrowMultiPipelineFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(() => dispatcher.Send(new MultiRequest()))
            .Throws<MultiPipelineFoundException>()
            .WithMessageContaining(typeof(MultiRequest).FullName!);
    }

    [Test]
    public async Task When_SendAsync_AndMultipleHandlersRegistered_Should_ThrowMultiPipelineFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.SendAsync(new MultiRequest()))
            .Throws<MultiPipelineFoundException>()
            .WithMessageContaining(typeof(MultiRequest).FullName!);
    }

    private class ExecutedRequests
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyCollection<string> Requests => _requests.ToArray();

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

    private class FirstMultiRequestHandler : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class SecondMultiRequestHandler : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
