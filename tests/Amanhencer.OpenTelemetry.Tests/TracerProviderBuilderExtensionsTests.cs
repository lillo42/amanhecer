using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using global::OpenTelemetry;
using global::OpenTelemetry.Trace;

namespace Amanhencer.OpenTelemetry.Tests;

[NotInParallel]
public class TracerProviderBuilderExtensionsTests
{
    private const string RoutingKeyTag = "amanhencer.routing_key";

    [Test]
    public async Task AddAmanhencerInstrumentation_NullBuilder_Throws()
    {
        TracerProviderBuilder builder = null!;

        await Assert.That(() => builder.AddAmanhencerInstrumentation())
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task AddAmanhencerInstrumentation_ValidBuilder_ReturnsSameBuilderForChaining()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        var result = builder.AddAmanhencerInstrumentation();

        await Assert.That(ReferenceEquals(result, builder)).IsTrue();
    }

    [Test]
    public async Task AddAmanhencerInstrumentation_DispatchedRequest_ExportsSpanWithExpectedNameAndTags()
    {
        const string routingKey = "otel.traces.success";
        var dispatcher = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<PlaceOrderHandler>()));
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddAmanhencerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        await dispatcher.SendAsync(new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey });
        provider.ForceFlush();

        var spans = exported.Where(a => a.OperationName == $"{routingKey} process").ToArray();
        await Assert.That(spans.Length).IsEqualTo(1);

        var span = spans[0];
        await Assert.That(span.Source.Name).IsEqualTo(AmanhencerDiagnostics.InstrumentationName);
        await Assert.That(span.Kind).IsEqualTo(ActivityKind.Internal);
        await Assert.That(span.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(span.GetTagItem(RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(span.GetTagItem("amanhencer.request.type")).IsEqualTo(typeof(PlaceOrder).FullName);
        await Assert.That(span.GetTagItem("amanhencer.operation")).IsEqualTo("send");
    }

    [Test]
    public async Task AddAmanhencerInstrumentation_FailedRequest_ExportsErrorSpanWithExceptionEvent()
    {
        const string routingKey = "otel.traces.failed";
        var dispatcher = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<ExplodingOrderHandler>()));
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddAmanhencerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        await Assert.That(async () => await dispatcher.SendAsync(
                new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey }))
            .ThrowsExactly<InvalidOperationException>();
        provider.ForceFlush();

        var spans = exported.Where(a => a.OperationName == $"{routingKey} process").ToArray();
        await Assert.That(spans.Length).IsEqualTo(1);
        await Assert.That(spans[0].Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(spans[0].Events.Any(e => e.Name == "exception")).IsTrue();
    }

    [Test]
    public async Task AddAmanhencerInstrumentation_ActivityFromOtherSource_IsNotCaptured()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddAmanhencerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();
        using var otherSource = new ActivitySource("Some.Other.Library");

        using (otherSource.StartActivity("other operation"))
        {
        }

        provider.ForceFlush();

        await Assert.That(exported).IsEmpty();
    }
}
