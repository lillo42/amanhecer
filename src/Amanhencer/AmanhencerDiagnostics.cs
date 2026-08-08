using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Amanhencer;

/// <summary>
/// Holds the shared telemetry instruments used by Amanhencer: the <see cref="System.Diagnostics.ActivitySource"/>
/// for tracing and the <see cref="System.Diagnostics.Metrics.Meter"/> for metrics.
/// </summary>
public static class AmanhencerDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> and
    /// <see cref="System.Diagnostics.Metrics.Meter"/> used by Amanhencer.
    /// </summary>
    public const string InstrumentationName = "Amanhencer";

    private static readonly string Version =
        typeof(AmanhencerDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> from which Amanhencer spans are started.
    /// Subscribe with an <see cref="ActivityListener"/> (or an OpenTelemetry tracer provider) using
    /// <see cref="InstrumentationName"/> to capture them.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(InstrumentationName, Version);

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> on which Amanhencer instruments are created.
    /// Subscribe with a <see cref="MeterListener"/> (or an OpenTelemetry meter provider) using
    /// <see cref="InstrumentationName"/> to capture them.
    /// </summary>
    public static readonly Meter Meter = new(InstrumentationName, Version);
}
