using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Amanhencer.IntegrationTests;

public sealed record Measurement(string Instrument, double Value, KeyValuePair<string, object?>[] Tags);

public sealed class MetricCapture : IDisposable
{
    private const string RoutingKeyTag = "amanhencer.routing_key";

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
