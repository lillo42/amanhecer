using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Extension methods to collect Amanhencer telemetry in an OpenTelemetry
/// <see cref="OpenTelemetryBuilder"/>.
/// </summary>
public static class AmanhencerOpenTelemetryBuilderExtensions
{
    /// <summary>
    /// Subscribes to the Amanhencer activity source and meter, collecting the spans,
    /// request counters and processing-duration histogram produced by the Amanhencer
    /// telemetry middleware.
    /// </summary>
    /// <param name="builder">The <see cref="OpenTelemetryBuilder"/> to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static OpenTelemetryBuilder AddAmanhencerInstrumentation(this OpenTelemetryBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder
            .WithTracing(tracing => tracing.AddAmanhencerInstrumentation())
            .WithMetrics(metrics => metrics.AddAmanhencerInstrumentation());
    }
}
