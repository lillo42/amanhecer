using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using NSubstitute;

namespace Amanhencer.Tests.Middlewares;

[NotInParallel]
public class AmanhencerTelemetryMiddlewareTests
{
    private const string RoutingKeyTag = "amanhencer.routing_key";
    private const string SuccessInstrument = "amanhencer.request.processing.success";

    private readonly AmanhencerTelemetryMiddleware _middleware = new();

    private static IPipelineContext CreateContext(string routingKey,
        List<KeyValuePair<string, object?>>? telemetryTags = null)
    {
        var context = Substitute.For<IPipelineContext>();
        context.RoutingKey.Returns(routingKey);
        context.Request.Returns(new SomeRequest());
        context.ExecutingStrategy.Returns(Substitute.For<IExecutingStrategy>());
        context.TelemetryTags.Returns(telemetryTags ?? []);
        context.DeepClone(Arg.Any<Activity>(), Arg.Any<CancellationToken>()).Returns(context);
        return context;
    }

    private static ActivityListener ListenForSpans(List<Activity> started)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    // NOTE: the "no ActivityListener" path (StartActivity returning null, so no DeepClone)
    // cannot be exercised here: the TUnit test host registers its own all-sources
    // ActivityListener, so AmanhencerDiagnostics.ActivitySource always has listeners.
    [Test]
    public async Task When_ExecuteAsync_Should_RecordSuccessMetricAndInvokeNext()
    {
        const string routingKey = "unit.telemetry.no-listener";
        var context = CreateContext(routingKey);
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        var recordedRoutingKeys = new List<string?>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AmanhencerDiagnostics.InstrumentationName &&
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
        using var parent = AmanhencerDiagnostics.ActivitySource.StartActivity("unit-parent");

        var context = CreateContext(routingKey);
        context.Activity.Returns(parent);
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var span = started.Single(a => a.OperationName == $"{routingKey} process");
        await Assert.That(span.ParentId).IsEqualTo(parent!.Id);
        context.Received(1).DeepClone(Arg.Is<Activity>(a => ReferenceEquals(a, span)),
            Arg.Any<CancellationToken>());
        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_WithTelemetryTags_Should_MergeTagsIntoActivity()
    {
        const string routingKey = "unit.telemetry.tags";
        var started = new List<Activity>();
        using var listener = ListenForSpans(started);

        var context = CreateContext(routingKey, [new KeyValuePair<string, object?>("tenant", "acme")]);
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var span = started.Single(a => a.OperationName == $"{routingKey} process");
        await Assert.That(span.GetTagItem("tenant")).IsEqualTo("acme");
        await Assert.That(span.GetTagItem(RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(span.GetTagItem("amanhencer.request.type"))
            .IsEqualTo(typeof(SomeRequest).FullName);
    }

    private sealed record SomeRequest;
}
