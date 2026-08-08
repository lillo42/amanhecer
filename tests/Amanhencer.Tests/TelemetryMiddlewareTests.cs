using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;

namespace Amanhencer.Tests;

[NotInParallel]
public class TelemetryMiddlewareTests
{
    private const string SuccessInstrument = "amanhencer.request.processing.success";
    private const string FailedInstrument = "amanhencer.request.processing.failed";
    private const string TimeoutInstrument = "amanhencer.request.processing.timeout";
    private const string CancelledInstrument = "amanhencer.request.processing.cancelled";
    private const string DurationInstrument = "amanhencer.request.processing.duration";
    private const string RoutingKeyTag = "amanhencer.routing_key";

    private sealed record Measurement(string Instrument, double Value, KeyValuePair<string, object?>[] Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener;

        private ConcurrentBag<Measurement> Measurements { get; } = new();

        public MetricCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter == AmanhencerDiagnostics.Meter)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
                Measurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Measurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            _listener.Start();
        }

        public IReadOnlyList<Measurement> For(string instrument, string routingKey)
        {
            return Measurements
                .Where(m => m.Instrument == instrument &&
                            m.Tags.Any(t => t.Key == RoutingKeyTag && (string?)t.Value == routingKey))
                .ToArray();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static object? Tag(Measurement measurement, string key)
    {
        return measurement.Tags.SingleOrDefault(t => t.Key == key).Value;
    }

    [Test]
    public async Task Success_RecordsSuccessCounterWithBaseTags_AndDuration()
    {
        const string routingKey = "telemetry.success";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await middleware.ExecuteAsync(TestPipelineContext.Create(routingKey: routingKey), _ => ValueTask.CompletedTask);

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        await Assert.That(success[0].Value).IsEqualTo(1);
        await Assert.That(Tag(success[0], "amanhencer.request.type")).IsEqualTo(typeof(TestRequest).FullName);
        await Assert.That(Tag(success[0], "amanhencer.executing_strategy"))
            .IsEqualTo(TestPipelineContext.Create().ExecutingStrategy.GetType().FullName);
        await Assert.That(Tag(success[0], "amanhencer.response.type")).IsNull();

        var duration = capture.For(DurationInstrument, routingKey);
        await Assert.That(duration.Count).IsEqualTo(1);
        await Assert.That(capture.For(FailedInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(TimeoutInstrument, routingKey)).IsEmpty();
    }

    [Test]
    public async Task Success_WithResponse_AddsResponseTypeTag()
    {
        const string routingKey = "telemetry.success.response";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await middleware.ExecuteAsync(TestPipelineContext.Create(routingKey: routingKey), context =>
        {
            context.Response = "response";
            return ValueTask.CompletedTask;
        });

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        await Assert.That(Tag(success[0], "amanhencer.response.type")).IsEqualTo(typeof(string).FullName);
    }

    [Test]
    public async Task GenericException_RecordsFailedCounterWithExceptionTag_AndRethrows()
    {
        const string routingKey = "telemetry.failed";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new InvalidOperationException("Boom.")))
            .ThrowsExactly<InvalidOperationException>();

        var failed = capture.For(FailedInstrument, routingKey);
        await Assert.That(failed.Count).IsEqualTo(1);
        await Assert.That(Tag(failed[0], "exception")).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(capture.For(SuccessInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(TimeoutInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);
    }

    [Test]
    public async Task TimeoutException_RecordsTimeoutCounter_AndRethrows()
    {
        const string routingKey = "telemetry.timeout";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new TimeoutException("Too slow.")))
            .ThrowsExactly<TimeoutException>();

        var timeout = capture.For(TimeoutInstrument, routingKey);
        await Assert.That(timeout.Count).IsEqualTo(1);
        await Assert.That(Tag(timeout[0], "exception")).IsEqualTo(typeof(TimeoutException).FullName);
        await Assert.That(capture.For(FailedInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);
    }

    [Test]
    public async Task OperationCanceledException_RecordsCancelled_AndRethrows()
    {
        const string routingKey = "telemetry.cancelled";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new OperationCanceledException("Cancelled.")))
            .ThrowsExactly<OperationCanceledException>();

        var cancelled = capture.For(CancelledInstrument, routingKey);
        await Assert.That(cancelled.Count).IsEqualTo(1);
        await Assert.That(Tag(cancelled[0], "exception")).IsEqualTo(typeof(OperationCanceledException).FullName);
        await Assert.That(capture.For(TimeoutInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(FailedInstrument, routingKey)).IsEmpty();
    }

    [Test]
    public async Task TelemetryTags_AreAddedToMetricTags()
    {
        const string routingKey = "telemetry.tags";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();
        var context = TestPipelineContext.Create(routingKey: routingKey);
        context.TelemetryTags.Add(new KeyValuePair<string, object?>("team", "core"));
        context.TelemetryTags.Add(new KeyValuePair<string, object?>("priority", 1));

        await middleware.ExecuteAsync(context, _ => ValueTask.CompletedTask);

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        await Assert.That(Tag(success[0], "team")).IsEqualTo("core");
        await Assert.That(Tag(success[0], "priority")).IsEqualTo(1);
    }

    [Test]
    public async Task Activity_WithoutListener_StillRecordsMetrics()
    {
        const string routingKey = "telemetry.no-listener";
        using var capture = new MetricCapture();
        var middleware = new AmanhencerTelemetryMiddleware();

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey), _ => ValueTask.CompletedTask))
            .ThrowsNothing();

        await Assert.That(capture.For(SuccessInstrument, routingKey).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Activity_WithListener_IsCreatedTaggedAndStopped()
    {
        const string routingKey = "telemetry.activity";
        var middleware = new AmanhencerTelemetryMiddleware();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);
        var context = TestPipelineContext.Create(routingKey: routingKey);
        context.TelemetryTags.Add(new KeyValuePair<string, object?>("team", "core"));
        IPipelineContext? nextContext = null;

        await middleware.ExecuteAsync(context, c =>
        {
            nextContext = c;
            c.Response = "response";
            return ValueTask.CompletedTask;
        });

        await Assert.That(stopped).IsNotNull();
        await Assert.That(stopped!.OperationName).IsEqualTo($"{routingKey} process");
        await Assert.That(stopped.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(stopped.StatusDescription).IsNull();
        await Assert.That(stopped.GetTagItem(RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(stopped.GetTagItem("amanhencer.request.type")).IsEqualTo(typeof(TestRequest).FullName);
        await Assert.That(stopped.GetTagItem("amanhencer.executing_strategy"))
            .IsEqualTo(context.ExecutingStrategy.GetType().FullName);
        await Assert.That(stopped.GetTagItem("team")).IsEqualTo("core");
        await Assert.That(stopped.GetTagItem("amanhencer.response.type")).IsNull();

        // The middleware passes a deep-cloned context carrying the started activity to the rest of the pipeline.
        await Assert.That(nextContext).IsNotNull();
        await Assert.That(ReferenceEquals(stopped, nextContext!.Activity)).IsTrue();
    }

    [Test]
    public async Task Activity_OnGenericException_SetsErrorStatusAndAddsExceptionEvent()
    {
        const string routingKey = "telemetry.activity.failed";
        var middleware = new AmanhencerTelemetryMiddleware();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new InvalidOperationException("Boom.")))
            .ThrowsExactly<InvalidOperationException>();

        await Assert.That(stopped).IsNotNull();
        await Assert.That(stopped!.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(stopped.StatusDescription).IsEqualTo("Boom.");
        await Assert.That(stopped.Events.Any(e => e.Name == "exception")).IsTrue();
    }

    [Test]
    public async Task Activity_OnTimeout_SetsErrorStatusWithTimeoutDescription()
    {
        const string routingKey = "telemetry.activity.timeout";
        var middleware = new AmanhencerTelemetryMiddleware();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new TimeoutException("Too slow.")))
            .ThrowsExactly<TimeoutException>();

        await Assert.That(stopped).IsNotNull();
        await Assert.That(stopped!.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(stopped.StatusDescription).IsEqualTo("Too slow.");
        await Assert.That(stopped.Events.Any(e => e.Name == "exception")).IsTrue();
    }

    [Test]
    public async Task Activity_OnCancelled_SetsErrorStatusWithCancelledDescription()
    {
        const string routingKey = "telemetry.activity.cancelled";
        var middleware = new AmanhencerTelemetryMiddleware();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: routingKey),
                _ => throw new OperationCanceledException("Cancelled.")))
            .ThrowsExactly<OperationCanceledException>();

        await Assert.That(stopped).IsNotNull();
        await Assert.That(stopped!.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(stopped.StatusDescription).IsEqualTo("Cancelled.");
        await Assert.That(stopped.Events.Any(e => e.Name == "exception")).IsTrue();
    }

    [Test]
    public async Task Attribute_ReturnsTelemetryMiddlewareType_AndKeepsOrder()
    {
        var attribute = new AmanhencerTelemetryAttribute(5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(AmanhencerTelemetryMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
    }
}
