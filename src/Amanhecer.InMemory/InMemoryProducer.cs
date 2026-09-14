using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amanhecer.Abstractions.Extensions;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// An <see cref="IProducer"/> that publishes messages into an in-memory queue.
/// </summary>
public class InMemoryProducer(QueueManagement queues) : IProducer
{
    /// <summary>Counts messages published successfully.</summary>
    private static readonly Counter<int> SuccessCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.publish.success",
        unit: "{message}",
        description: "Number of messages published successfully.");

    /// <summary>Counts messages that failed to publish with an exception.</summary>
    private static readonly Counter<int> FailedCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.publish.failed",
        unit: "{message}",
        description: "Number of messages that failed to publish.");

    /// <summary>Records how long publishing a message took, in seconds.</summary>
    private static readonly Histogram<double> ProducerDuration =
        AmanhecerDiagnostics.Meter.CreateHistogram<double>(
            "amanhecer.message.publish.duration",
            unit: "s",
            description: "Duration of message publishing, in seconds.");

    /// <inheritdoc/>
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context)
    {
        if (publication is not InMemoryPublication inMemoryPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(InMemoryPublication)}.",
                nameof(publication));
        }

        var queueName = inMemoryPublication.ResolvedQueueName;

        // Low-cardinality tags shared by the metrics instruments (OTel messaging conventions).
        var metricTags = new List<KeyValuePair<string, object?>>
        {
            new("messaging.system", "inmemory"),
            new("messaging.operation.type", "publish"),
            new("messaging.destination.name", queueName),
        }.ToArray();

        // Per-message values are high-cardinality, so they go on the span only, never on metrics.
        var spanTags = new List<KeyValuePair<string, object?>>(metricTags)
        {
            new("messaging.message.id", message.Id),
            new("messaging.message.conversation_id", message.CorrelationId),
            new("cloudevents.event_id", message.Id),
            new("cloudevents.event_source", message.Source?.ToString()),
            new("cloudevents.event_spec_version", message.SpecVersion),
            new("cloudevents.event_type", message.Type),
        }.ToArray();

        var activity = AmanhecerDiagnostics.ActivitySource.StartActivity(
            "Producer",
            ActivityKind.Producer,
            parentContext: context.Activity?.Context ?? default,
            tags: spanTags);

        message.Enrich(activity);

        BinaryCloudEventHeaders.Apply(message, inMemoryPublication);

        var duration = Stopwatch.StartNew();
        try
        {
            // Inside the try so a queue that was never provisioned is recorded by the metrics
            // and the span, instead of escaping before either is settled.
            var channel = queues.GetChannels(queueName);

            await channel.Writer.WriteAsync(message, context.CancellationToken)
                .ConfigureAwait(context.ContinueOnCapturedContext);

            duration.Stop();
            SuccessCounter.Add(1, metricTags);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception e)
        {
            duration.Stop();
            FailedCounter.Add(1, metricTags);
#if !NET8_0
            activity?.AddException(e);
#endif
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
        finally
        {
            ProducerDuration.Record(duration.Elapsed.TotalSeconds, metricTags);
            activity?.Stop();
        }
    }
}
