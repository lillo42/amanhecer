using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Amanhecer;

/// <summary>
/// Holds the shared telemetry instruments used by Amanhecer: the <see cref="System.Diagnostics.ActivitySource"/>
/// for tracing and the <see cref="System.Diagnostics.Metrics.Meter"/> for metrics.
/// </summary>
public static class AmanhecerDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> and
    /// <see cref="System.Diagnostics.Metrics.Meter"/> used by Amanhecer.
    /// </summary>
    public const string InstrumentationName = "Amanhecer";

    private static readonly string Version =
        typeof(AmanhecerDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> from which Amanhecer spans are started.
    /// Subscribe with an <see cref="ActivityListener"/> (or an OpenTelemetry tracer provider) using
    /// <see cref="InstrumentationName"/> to capture them.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(InstrumentationName, Version);

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> on which Amanhecer instruments are created.
    /// Subscribe with a <see cref="MeterListener"/> (or an OpenTelemetry meter provider) using
    /// <see cref="InstrumentationName"/> to capture them.
    /// </summary>
    public static readonly Meter Meter = new(InstrumentationName, Version);
}
