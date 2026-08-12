using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Middlewares;
using global::OpenTelemetry;
using global::OpenTelemetry.Metrics;

namespace Amanhecer.OpenTelemetry.Tests;

[NotInParallel]
public class MeterProviderBuilderExtensionsTests
{
    private const string SuccessInstrument = "amanhecer.request.processing.success";
    private const string FailedInstrument = "amanhecer.request.processing.failed";
    private const string DurationInstrument = "amanhecer.request.processing.duration";
    private const string RoutingKeyTag = "amanhecer.routing_key";

    [Test]
    public async Task AddAmanhecerInstrumentation_NullBuilder_Throws()
    {
        MeterProviderBuilder builder = null!;

        await Assert.That(() => builder.AddAmanhecerInstrumentation())
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task AddAmanhecerInstrumentation_ValidBuilder_ReturnsSameBuilderForChaining()
    {
        var builder = Sdk.CreateMeterProviderBuilder();

        var result = builder.AddAmanhecerInstrumentation();

        await Assert.That(ReferenceEquals(result, builder)).IsTrue();
    }

    [Test]
    public async Task AddAmanhecerInstrumentation_DispatchedRequest_ExportsSuccessCounterAndDurationHistogram()
    {
        const string routingKey = "otel.metrics.success";
        var dispatcher = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhecerTelemetryMiddleware>(order: 1)
                .UseHandler<PlaceOrderHandler>()));
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddAmanhecerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        await dispatcher.SendAsync(new PlaceOrder("apple"), new AmanhecerContext { RoutingKey = routingKey });
        provider.ForceFlush();

        var success = exported.SingleOrDefault(m => m.Name == SuccessInstrument);
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.MeterName).IsEqualTo(AmanhecerDiagnostics.InstrumentationName);

        var successPoints = Points(success);
        await Assert.That(successPoints.Count).IsEqualTo(1);
        await Assert.That(successPoints[0].GetSumLong()).IsEqualTo(1);
        await Assert.That(HasRoutingKey(successPoints[0], routingKey)).IsTrue();

        var duration = exported.SingleOrDefault(m => m.Name == DurationInstrument);
        await Assert.That(duration).IsNotNull();

        var durationPoints = Points(duration!);
        await Assert.That(durationPoints.Count).IsEqualTo(1);
        await Assert.That(durationPoints[0].GetHistogramCount()).IsEqualTo(1);
        await Assert.That(HasRoutingKey(durationPoints[0], routingKey)).IsTrue();

        await Assert.That(exported.Any(m => m.Name == FailedInstrument)).IsFalse();
    }

    [Test]
    public async Task AddAmanhecerInstrumentation_FailedRequest_ExportsFailedCounter()
    {
        const string routingKey = "otel.metrics.failed";
        var dispatcher = DispatcherFixture.Create(cfg =>
            cfg.AddRoutingKey(routingKey, routing => routing
                .Use<AmanhecerTelemetryMiddleware>(order: 1)
                .UseHandler<ExplodingOrderHandler>()));
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddAmanhecerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        await Assert.That(async () => await dispatcher.SendAsync(
                new PlaceOrder("apple"), new AmanhecerContext { RoutingKey = routingKey }))
            .ThrowsExactly<InvalidOperationException>();
        provider.ForceFlush();

        var failed = exported.SingleOrDefault(m => m.Name == FailedInstrument);
        await Assert.That(failed).IsNotNull();

        var failedPoints = Points(failed!);
        await Assert.That(failedPoints.Count).IsEqualTo(1);
        await Assert.That(failedPoints[0].GetSumLong()).IsEqualTo(1);
        await Assert.That(HasRoutingKey(failedPoints[0], routingKey)).IsTrue();

        await Assert.That(exported.Any(m => m.Name == SuccessInstrument)).IsFalse();
    }

    [Test]
    public async Task AddAmanhecerInstrumentation_InstrumentFromOtherMeter_IsNotCaptured()
    {
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddAmanhecerInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();
        using var otherMeter = new Meter("Some.Other.Library");
        var counter = otherMeter.CreateCounter<long>("other.counter");

        counter.Add(42);
        provider.ForceFlush();

        await Assert.That(exported.Any(m => m.Name == "other.counter")).IsFalse();
    }

    private static List<MetricPoint> Points(Metric metric)
    {
        var points = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            points.Add(point);
        }

        return points;
    }

    private static bool HasRoutingKey(MetricPoint point, string routingKey)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == RoutingKeyTag && (string?)tag.Value == routingKey)
            {
                return true;
            }
        }

        return false;
    }
}
