using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Configurator;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.IntegrationTests;

public class RoutingKeyTests : BaseTests
{
    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<HandledRequests>();
    }

    protected override void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<PriorityOrderHandler>()
            .AddQueryHandler<PriorityLookupHandler>()
            .AddRoutingKey("custom.key", routing => routing
                .UseHandler<PlainOrderHandler>())
            .AddRoutingKey("attributed.key", routing => routing
                .UseHandler<AttributedKeyOrderHandler>())
            .AddRoutingKey("override.key", routing => routing
                .UseHandler<OverrideKeyOrderHandler>())
            .AddRoutingKey("shared", routing => routing
                .UseHandler<FirstSharedHandler>())
            .AddRoutingKey("shared", routing => routing
                .UseHandler<SecondSharedHandler>());
    }

    [Test]
    public async Task When_Send_Should_RouteByRoutingKeyAttribute()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new PriorityOrder());

        var handled = ServiceProvider.GetRequiredService<HandledRequests>();
        await Assert.That(handled.Requests).Contains(nameof(PriorityOrderHandler));
    }

    [Test]
    public async Task When_Query_Should_RouteByRoutingKeyAttribute()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        var response = await dispatcher.QueryAsync<PriorityLookup, string>(new PriorityLookup());

        await Assert.That(response).IsEqualTo("priority-lookup");
    }

    [Test]
    public async Task When_Send_Should_RouteByExplicitContextRoutingKey()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new PlainOrder(), new AmanhecerContext { RoutingKey = "custom.key" });

        var handled = ServiceProvider.GetRequiredService<HandledRequests>();
        await Assert.That(handled.Requests).Contains(nameof(PlainOrderHandler));
    }

    [Test]
    public async Task When_Send_Should_PreferContextRoutingKeyOverAttribute()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new OverrideOrder(), new AmanhecerContext { RoutingKey = "override.key" });

        var handled = ServiceProvider.GetRequiredService<HandledRequests>();
        await Assert.That(handled.Requests)
            .Contains(nameof(OverrideKeyOrderHandler))
            .And.DoesNotContain(nameof(AttributedKeyOrderHandler));
    }

    [Test]
    public async Task When_Publish_Should_FanOutToAllPipelinesSharingRoutingKey()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.PublishAsync(new SharedRequest());

        var handled = ServiceProvider.GetRequiredService<HandledRequests>();
        await Assert.That(handled.Requests)
            .Contains(nameof(FirstSharedHandler))
            .And.Contains(nameof(SecondSharedHandler));
    }

    private class HandledRequests
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyCollection<string> Requests => [.. _requests];

        public void Add(string handler) => _requests.Enqueue(handler);
    }

    [RoutingKey("priority.order")]
    private record PriorityOrder;

    private class PriorityOrderHandler(HandledRequests handled) : RequestHandler<PriorityOrder>
    {
        public override ValueTask HandleAsync(PriorityOrder request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(PriorityOrderHandler));
            return ValueTask.CompletedTask;
        }
    }

    [RoutingKey("priority.lookup")]
    private record PriorityLookup;

    private class PriorityLookupHandler : QueryHandler<PriorityLookup, string>
    {
        public override ValueTask<string> HandleAsync(PriorityLookup query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult("priority-lookup");
        }
    }

    private record PlainOrder;

    private class PlainOrderHandler(HandledRequests handled) : RequestHandler<PlainOrder>
    {
        public override ValueTask HandleAsync(PlainOrder request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(PlainOrderHandler));
            return ValueTask.CompletedTask;
        }
    }

    [RoutingKey("attributed.key")]
    private record OverrideOrder;

    private class AttributedKeyOrderHandler(HandledRequests handled) : RequestHandler<OverrideOrder>
    {
        public override ValueTask HandleAsync(OverrideOrder request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(AttributedKeyOrderHandler));
            return ValueTask.CompletedTask;
        }
    }

    private class OverrideKeyOrderHandler(HandledRequests handled) : RequestHandler<OverrideOrder>
    {
        public override ValueTask HandleAsync(OverrideOrder request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(OverrideKeyOrderHandler));
            return ValueTask.CompletedTask;
        }
    }

    [RoutingKey("shared")]
    private record SharedRequest;

    private class FirstSharedHandler(HandledRequests handled) : RequestHandler<SharedRequest>
    {
        public override ValueTask HandleAsync(SharedRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(FirstSharedHandler));
            return ValueTask.CompletedTask;
        }
    }

    private class SecondSharedHandler(HandledRequests handled) : RequestHandler<SharedRequest>
    {
        public override ValueTask HandleAsync(SharedRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            handled.Add(nameof(SecondSharedHandler));
            return ValueTask.CompletedTask;
        }
    }
}
