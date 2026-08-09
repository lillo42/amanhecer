using System;
using Amanhencer;

// Resides in OpenTelemetry.Trace so the extension lights up alongside the other
// TracerProviderBuilder instrumentation extensions.
namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to collect Amanhencer traces in an OpenTelemetry
/// <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class AmanhencerTracerProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes to the Amanhencer <see cref="System.Diagnostics.ActivitySource"/>,
    /// collecting the spans produced by the Amanhencer telemetry middleware.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddAmanhencerInstrumentation(this TracerProviderBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddSource(AmanhencerDiagnostics.InstrumentationName);
    }
}
