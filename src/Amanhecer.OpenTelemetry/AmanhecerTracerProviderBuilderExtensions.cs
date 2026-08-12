using System;
using Amanhecer;

// Resides in OpenTelemetry.Trace so the extension lights up alongside the other
// TracerProviderBuilder instrumentation extensions.
namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to collect Amanhecer traces in an OpenTelemetry
/// <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class AmanhecerTracerProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes to the Amanhecer <see cref="System.Diagnostics.ActivitySource"/>,
    /// collecting the spans produced by the Amanhecer telemetry middleware.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddAmanhecerInstrumentation(this TracerProviderBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddSource(AmanhecerDiagnostics.InstrumentationName);
    }
}
