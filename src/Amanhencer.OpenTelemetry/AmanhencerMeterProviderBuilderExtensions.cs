using Amanhencer;

// Resides in OpenTelemetry.Metrics so the extension lights up alongside the other
// MeterProviderBuilder instrumentation extensions.
namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to collect Amanhencer metrics in an OpenTelemetry
/// <see cref="MeterProviderBuilder"/>.
/// </summary>
public static class AmanhencerMeterProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes to the Amanhencer <see cref="System.Diagnostics.Metrics.Meter"/>,
    /// collecting the request counters and processing-duration histogram produced by
    /// the Amanhencer telemetry middleware.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MeterProviderBuilder AddAmanhencerInstrumentation(this MeterProviderBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddMeter(AmanhencerDiagnostics.InstrumentationName);
    }
}
