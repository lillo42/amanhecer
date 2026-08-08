using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.Middlewares;


/// <summary>
/// Declares <see cref="AmanhencerTelemetryMiddleware"/> on the annotated handler class or
/// <c>HandleAsync</c> method, adding request telemetry to that handler's pipeline.
/// </summary>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
public class AmanhencerTelemetryAttribute(int order) : MiddlewareAttribute(order)
{
    /// <inheritdoc />
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType()
    {
        return typeof(AmanhencerTelemetryMiddleware);
    }
}

/// <summary>
/// Middleware that records telemetry for each request flowing through the pipeline: a span from
/// <see cref="AmanhencerDiagnostics.ActivitySource"/> (parented to <see cref="IPipelineContext.Activity"/>
/// when set) and success/failure/timeout/cancellation counters plus a processing-duration histogram
/// on <see cref="AmanhencerDiagnostics.Meter"/>.
/// </summary>
public class AmanhencerTelemetryMiddleware : IMiddleware
{
    /// <summary>Counts requests processed successfully.</summary>
    private static readonly Counter<int> SuccessCounter = AmanhencerDiagnostics.Meter.CreateCounter<int>(
        "amanhencer.request.processing.success",
        unit: "{request}",
        description: "Number of requests processed successfully.");

    /// <summary>Counts requests whose processing failed with an exception.</summary>
    private static readonly Counter<int> FailedCounter = AmanhencerDiagnostics.Meter.CreateCounter<int>(
        "amanhencer.request.processing.failed",
        unit: "{request}",
        description: "Number of requests that failed during processing.");

    /// <summary>Counts requests whose processing timed out.</summary>
    private static readonly Counter<int> TimeoutCounter = AmanhencerDiagnostics.Meter.CreateCounter<int>(
        "amanhencer.request.processing.timeout",
        unit: "{request}",
        description: "Number of requests that timed out during processing.");

    /// <summary>Counts requests whose processing was cancelled.</summary>
    private static readonly Counter<int> CancelledCounter = AmanhencerDiagnostics.Meter.CreateCounter<int>(
        "amanhencer.request.processing.cancelled",
        unit: "{request}",
        description: "Number of requests cancelled during processing.");

    /// <summary>Records how long request processing took, in seconds.</summary>
    private static readonly Histogram<double> ProcessingDuration =
        AmanhencerDiagnostics.Meter.CreateHistogram<double>(
            "amanhencer.request.processing.duration",
            unit: "s",
            description: "Duration of request processing, in seconds.");

    /// <inheritdoc />
    public void Initialize(object? metadata)
    {
    }

    /// <summary>
    /// Starts a span named <c>{routing key} process</c>, tags it and the metrics with the routing key,
    /// request type, executing strategy and <see cref="IPipelineContext.TelemetryTags"/>, then invokes
    /// the rest of the pipeline. Records the outcome (success, failure, timeout or cancellation) and
    /// the processing duration, and rethrows any exception.
    /// </summary>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished.</returns>
    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        var metadata = new List<KeyValuePair<string, object?>>
        {
            new("amanhencer.routing_key", context.RoutingKey),
            new("amanhencer.request.type", context.Request.GetType().FullName ?? context.Request.GetType().Name),
            new("amanhencer.executing_strategy",
                context.ExecutingStrategy.GetType().FullName ?? context.ExecutingStrategy.GetType().Name),
        };

        metadata.AddRange(context.TelemetryTags);

        var activity = AmanhencerDiagnostics.ActivitySource.StartActivity($"{context.RoutingKey} process",
            kind: ActivityKind.Internal,
            parentContext: context.Activity?.Context ?? default,
            tags: metadata);

        context = activity == null ? context : context.DeepClone(activity);

        var duration = Stopwatch.StartNew();

        try
        {
            await next(context);

            duration.Stop();

            if (context.Response != null)
            {
                metadata.Add(new KeyValuePair<string, object?>("amanhencer.response.type",
                    context.Response.GetType().FullName ?? context.Response.GetType().Name));
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            SuccessCounter.Add(1, metadata.ToArray());
        }
        catch (Exception exception)
        {
            duration.Stop();

            metadata.Add(new("exception", exception.GetType().FullName ?? exception.GetType().Name));


            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
#if !NET8_0
            activity?.AddException(exception);
#endif

            if (exception is TimeoutException)
            {
                TimeoutCounter.Add(1, metadata.ToArray());
            }
            else if (exception is OperationCanceledException)
            {
                CancelledCounter.Add(1, metadata.ToArray());
            }
            else
            {
                FailedCounter.Add(1, metadata.ToArray());
            }

            throw;
        }
        finally
        {
            activity?.Stop();

            ProcessingDuration.Record(duration.Elapsed.TotalSeconds, metadata.ToArray());
        }
    }
}