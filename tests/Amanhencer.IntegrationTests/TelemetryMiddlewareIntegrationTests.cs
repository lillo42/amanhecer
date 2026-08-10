using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Extensions;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amanhencer.IntegrationTests;

[NotInParallel]
public class TelemetryMiddlewareIntegrationTests
{
    private const string SuccessInstrument = "amanhencer.request.processing.success";
    private const string FailedInstrument = "amanhencer.request.processing.failed";
    private const string TimeoutInstrument = "amanhencer.request.processing.timeout";
    private const string CancelledInstrument = "amanhencer.request.processing.cancelled";
    private const string DurationInstrument = "amanhencer.request.processing.duration";
    private const string RoutingKeyTag = "amanhencer.routing_key";

    [AmanhencerTelemetry(1)]
    private class TelemetrisedOrderHandler(ExecutionLog log) : RequestHandler<PlaceOrder>
    {
        public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            log.Add($"telemetrised:{request.Product}");
            return ValueTask.CompletedTask;
        }
    }

    private static ActivityListener ListenForSpans(List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhencerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static object? Tag(Measurement measurement, string key)
    {
        return measurement.Tags.SingleOrDefault(t => t.Key == key).Value;
    }

    [Test]
    public async Task Success_RecordsMetricsAndSpan_EndToEnd()
    {
        const string routingKey = "integration.telemetry.success";
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<PlaceOrderHandler>()));
        using var capture = new MetricCapture();
        var stopped = new List<Activity>();
        using var listener = ListenForSpans(stopped);

        await dispatcher.SendAsync(new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey });

        await Assert.That(log.Entries).Contains("handled:apple");

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        await Assert.That(Tag(success[0], RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(Tag(success[0], "amanhencer.request.type")).IsEqualTo(typeof(PlaceOrder).FullName);
        await Assert.That(Tag(success[0], "amanhencer.operation")).IsEqualTo("send");
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);

        var spans = stopped.Where(a => Equals(a.GetTagItem(RoutingKeyTag), routingKey)).ToArray();
        await Assert.That(spans.Length).IsEqualTo(1);
        await Assert.That(spans[0].OperationName).IsEqualTo($"{routingKey} process");
        await Assert.That(spans[0].Status).IsEqualTo(ActivityStatusCode.Ok);
    }

    [Test]
    public async Task Failure_RecordsFailedCounterAndErrorSpan_EndToEnd()
    {
        const string routingKey = "integration.telemetry.failed";
        var (dispatcher, _) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<ExplodingOrderHandler>()));
        using var capture = new MetricCapture();
        var stopped = new List<Activity>();
        using var listener = ListenForSpans(stopped);

        await Assert.That(async () => await dispatcher.SendAsync(
                new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey }))
            .ThrowsExactly<InvalidOperationException>();

        var failed = capture.For(FailedInstrument, routingKey);
        await Assert.That(failed.Count).IsEqualTo(1);
        await Assert.That(Tag(failed[0], "exception")).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(capture.For(SuccessInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);

        var spans = stopped.Where(a => Equals(a.GetTagItem(RoutingKeyTag), routingKey)).ToArray();
        await Assert.That(spans.Length).IsEqualTo(1);
        await Assert.That(spans[0].Status).IsEqualTo(ActivityStatusCode.Error);
    }

    [Test]
    public async Task Attribute_RegisteredMiddleware_RecordsMetricsAndSpan()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg => cfg.AddRequestHandler<TelemetrisedOrderHandler>());
        var routingKey = typeof(PlaceOrder).FullName!;
        using var capture = new MetricCapture();
        var stopped = new List<Activity>();
        using var listener = ListenForSpans(stopped);

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Entries).Contains("telemetrised:apple");
        await Assert.That(capture.For(SuccessInstrument, routingKey).Count).IsEqualTo(1);

        var spans = stopped.Where(a => Equals(a.GetTagItem(RoutingKeyTag), routingKey)).ToArray();
        await Assert.That(spans.Length).IsEqualTo(1);
        await Assert.That(spans[0].Status).IsEqualTo(ActivityStatusCode.Ok);
    }

    [Test]
    public async Task Query_RecordsOperationResponseTypeAndStrategyTags_ButResponseIsLost()
    {
        const string routingKey = "integration.telemetry.query";
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<GetStockHandler>()));
        using var capture = new MetricCapture();
        var stopped = new List<Activity>();
        using var listener = ListenForSpans(stopped);

        // Pinned, but clearly broken: when an ActivityListener is attached (always the case in an
        // OpenTelemetry-instrumented app), AmanhencerTelemetryMiddleware deep-clones the context
        // for the span and the handler's response is stored on the clone — QueryAsync then reads
        // the original context's null Response and throws NullReferenceException instead of
        // returning the handler's result. The handler still runs and the success counter is still
        // recorded (against the clone), including the response-type tag.
        await Assert.That(async () => await dispatcher.QueryAsync<GetStock, int>(new GetStock("apple"),
                new AmanhencerContext { RoutingKey = routingKey }))
            .ThrowsExactly<NullReferenceException>();
        await Assert.That(log.Entries).Contains("queried:apple");

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        await Assert.That(Tag(success[0], "amanhencer.operation")).IsEqualTo("query");
        await Assert.That(Tag(success[0], "amanhencer.response.type")).IsEqualTo(typeof(int).FullName);
        await Assert.That(Tag(success[0], "amanhencer.executing_strategy"))
            .IsEqualTo(typeof(SequenceExecutingStrategy).FullName);
    }

    [Test]
    public async Task Publish_RecordsOperationAndStrategyTags_EndToEnd()
    {
        const string routingKey = "integration.telemetry.publish";
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<FirstShippedHandler>()));
        using var capture = new MetricCapture();

        await dispatcher.PublishAsync(new OrderShipped("book"), new AmanhencerContext { RoutingKey = routingKey });

        await Assert.That(log.Entries).Contains("first:book");

        var success = capture.For(SuccessInstrument, routingKey);
        await Assert.That(success.Count).IsEqualTo(1);
        // Pinned current value: AmanhencerDispatcher tags publish dispatches with operation
        // "post" (not "publish").
        await Assert.That(Tag(success[0], "amanhencer.operation")).IsEqualTo("post");
        await Assert.That(Tag(success[0], "amanhencer.executing_strategy"))
            .IsEqualTo(typeof(SequenceExecutingStrategy).FullName);
    }

    [Test]
    public async Task Timeout_RecordsTimeoutCounter_EndToEnd()
    {
        const string routingKey = "integration.telemetry.timeout";
        var (dispatcher, _) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<TimeoutOrderHandler>()));
        using var capture = new MetricCapture();

        await Assert.That(async () => await dispatcher.SendAsync(
                new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey }))
            .ThrowsExactly<TimeoutException>();

        var timeout = capture.For(TimeoutInstrument, routingKey);
        await Assert.That(timeout.Count).IsEqualTo(1);
        await Assert.That(Tag(timeout[0], RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(Tag(timeout[0], "exception")).IsEqualTo(typeof(TimeoutException).FullName);
        await Assert.That(capture.For(FailedInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(SuccessInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Cancelled_RecordsCancelledCounter_EndToEnd()
    {
        const string routingKey = "integration.telemetry.cancelled";
        var (dispatcher, _) = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhencerTelemetryMiddleware>(order: 1)
                .UseHandler<CancelledOrderHandler>()));
        using var capture = new MetricCapture();

        await Assert.That(async () => await dispatcher.SendAsync(
                new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey }))
            .ThrowsExactly<OperationCanceledException>();

        var cancelled = capture.For(CancelledInstrument, routingKey);
        await Assert.That(cancelled.Count).IsEqualTo(1);
        await Assert.That(Tag(cancelled[0], RoutingKeyTag)).IsEqualTo(routingKey);
        await Assert.That(Tag(cancelled[0], "exception")).IsEqualTo(typeof(OperationCanceledException).FullName);
        await Assert.That(capture.For(FailedInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(SuccessInstrument, routingKey)).IsEmpty();
        await Assert.That(capture.For(DurationInstrument, routingKey).Count).IsEqualTo(1);
    }

    [Test]
    public async Task TelemetryAndLogger_Compose_OnTheSamePipeline()
    {
        const string routingKey = "integration.telemetry.compose";
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var executionLog = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(executionLog);
        services.AddSingleton<ILogger<AmanhencerLoggerMiddleware>>(logger);
        services.AddAmanhencer(cfg => cfg.AddRoutingKey(routingKey, routing => routing
            .Use<AmanhencerTelemetryMiddleware>(order: 1)
            .Use<AmanhencerLoggerMiddleware>(order: 2)
            .UseHandler<PlaceOrderHandler>()));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IDispatcher>();
        using var capture = new MetricCapture();

        await dispatcher.SendAsync(new PlaceOrder("apple"), new AmanhencerContext { RoutingKey = routingKey });

        await Assert.That(executionLog.Entries).Contains("handled:apple");
        await Assert.That(capture.For(SuccessInstrument, routingKey).Count).IsEqualTo(1);

        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Message)
            .IsEqualTo($"Processing {routingKey} request {typeof(PlaceOrder).FullName}");
        await Assert.That(entries[1].Message)
            .IsEqualTo($"Processed {routingKey} request {typeof(PlaceOrder).FullName}");
    }
}
