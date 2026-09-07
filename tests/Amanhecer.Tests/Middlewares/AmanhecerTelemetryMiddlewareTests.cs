using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Middlewares;
using NSubstitute;

namespace Amanhecer.Tests.Middlewares;

[NotInParallel]
public class AmanhecerTelemetryMiddlewareTests
{
    private const string RoutingKeyTag = "amanhecer.routing_key";
    private const string SuccessInstrument = "amanhecer.request.processing.success";

    private readonly AmanhecerTelemetryMiddleware _middleware = new();

    private static AmanhecerContext CreateContext(string routingKey,
        List<KeyValuePair<string, object?>>? telemetryTags = null)
    {
        return new AmanhecerContext
        {
            RoutingKey = routingKey,
            Request = new SomeRequest(),
            ExecutingStrategy = Substitute.For<IExecutingStrategy>(),
            TelemetryTags = telemetryTags ?? []
        };
    }

    private static ActivityListener ListenForSpans(List<Activity> started)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhecerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public async Task When_ExecuteAsync_Should_RecordSuccessMetricAndInvokeNext()
    {
        const string routingKey = "unit.telemetry.no-listener";
        var context = CreateContext(routingKey);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var recordedRoutingKeys = new List<string?>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AmanhecerDiagnostics.InstrumentationName &&
                instrument.Name == SuccessInstrument)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<int>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == RoutingKeyTag)
                {
                    recordedRoutingKeys.Add(tag.Value as string);
                }
            }
        });
        meterListener.Start();

        await _middleware.ExecuteAsync(context, next);

        await next.Received(1).Invoke(context);
        await Assert.That(recordedRoutingKeys).Contains(routingKey);
    }

    [Test]
    public async Task When_ExecuteAsync_WithContextActivity_Should_ParentSpanToContextActivity()
    {
        const string routingKey = "unit.telemetry.parent";
        var started = new List<Activity>();
        using var listener = ListenForSpans(started);
        using var parent = AmanhecerDiagnostics.ActivitySource.StartActivity("unit-parent");

        var context = CreateContext(routingKey);
        context.Activity = parent;
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var span = started.Single(a => a.OperationName == $"{routingKey} process");
        await Assert.That(span.ParentId).IsEqualTo(parent!.Id);
        await Assert.That(context.Activity).IsSameReferenceAs(parent);
        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_WithoutContextActivity_Should_AssignTheSpanToTheContext()
    {
        const string routingKey = "unit.telemetry.assign-span";
        var started = new List<Activity>();
        using var listener = ListenForSpans(started);

        var context = CreateContext(routingKey);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var span = started.Single(a => a.OperationName == $"{routingKey} process");
        await Assert.That(context.Activity).IsSameReferenceAs(span);
        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_WithTelemetryTags_Should_MergeTagsIntoActivity()
    {
        const string routingKey = "unit.telemetry.tags";
        var started = new List<Activity>();
        using var listener = ListenForSpans(started);

        var context = CreateContext(routingKey, [new KeyValuePair<string, object?>("tenant", "acme")]);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var span = started.Single(a => a.OperationName == $"{routingKey} process");
        await Assert.That(span.GetTagItem("tenant")).IsEqualTo("acme");
        await Assert.That(span.GetTagItem(RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(span.GetTagItem("amanhecer.request.type"))
            .IsEqualTo(typeof(SomeRequest).FullName);
    }

    private sealed record SomeRequest;
}
