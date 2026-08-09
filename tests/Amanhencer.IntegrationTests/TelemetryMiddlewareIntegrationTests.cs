using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Extensions;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Amanhencer.IntegrationTests;

[NotInParallel]
public class TelemetryMiddlewareIntegrationTests
{
    private const string SuccessInstrument = "amanhencer.request.processing.success";
    private const string FailedInstrument = "amanhencer.request.processing.failed";
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
