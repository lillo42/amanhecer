using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Amanhencer.IntegrationTests;

public class ContextPropagationTests
{
    private static AmanhencerContext TenantContext(string tenant) => new()
    {
        Metadata = { ["tenant"] = tenant }
    };

    [Test]
    public async Task Send_MetadataIsObservableInMiddlewareAndHandler()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<TenantRequestHandler>(routing =>
            routing.Use<TenantMiddleware>(order: 1)));

        await dispatcher.SendAsync(new TenantRequest(), TenantContext("acme"));

        await Assert.That(log.Entries).Contains("middleware-tenant:acme");
        await Assert.That(log.Entries).Contains("handler-tenant:acme");
    }

    [Test]
    public async Task Publish_MetadataIsObservableInEachPipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<TenantRequest>(), routing => routing
                .Use<TenantMiddleware>(order: 1)
                .UseHandler<TenantRequestHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<TenantRequest>(), routing => routing
                .UseHandler<TenantRequestHandler>());
        });

        await dispatcher.PublishAsync(new TenantRequest(), TenantContext("acme"));

        await Assert.That(log.Entries).Contains("middleware-tenant:acme");
        await Assert.That(log.Entries.Count(e => e == "handler-tenant:acme")).IsEqualTo(2);
    }

    [Test]
    public async Task Query_MetadataFlowsIntoHandler()
    {
        var (dispatcher, _) = DispatcherFixture.Create(cfg => cfg.AddQueryHandler<TenantQueryHandler>());

        var tenant = await dispatcher.QueryAsync<TenantQuery, string>(new TenantQuery(), TenantContext("acme"));

        await Assert.That(tenant).IsEqualTo("acme");
    }

    [Test]
    public async Task Send_ExplicitRoutingKeyOnContext_RoutesToMatchingPipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey("custom.key", routing => routing.UseHandler<PriorityOrderHandler>()));

        await dispatcher.SendAsync(new PriorityOrder("apple"), new AmanhencerContext { RoutingKey = "custom.key" });

        await Assert.That(log.Entries).Contains("priority:apple");
    }

    [Test]
    public async Task Send_RoutingKeyAttribute_RoutesToAttributePipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey("priority.order", routing => routing.UseHandler<PriorityOrderHandler>()));

        await dispatcher.SendAsync(new PriorityOrder("apple"));

        await Assert.That(log.Entries).Contains("priority:apple");
    }
}
