using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Stream.Client.AMQP;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams;

/// <summary>RabbitMQ Streams implementation of <see cref="IProducer"/> that publishes messages via a <see cref="Producer"/>.</summary>
/// <param name="producer">The underlying reliable producer used to send messages.</param>
public class RabbitMqStreamProducer(Producer producer) : IProducer
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
        if (publication is not RabbitMqStreamPublication rabbitMqPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(RabbitMqStreamPublication)}.",
                nameof(publication));
        }

        var metricTags = new List<KeyValuePair<string, object?>>
        {
            new("messaging.system", "rabbitmq"),
            new("messaging.operation.type", "publish"),
            new("messaging.destination.name", rabbitMqPublication.Stream),
        }.ToArray();

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

        SetCloudEventHeaders(message, publication);

        var duration = Stopwatch.StartNew();
        try
        {
            await producer.Send(ToRabbitMqMessage(message, rabbitMqPublication, context))
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

    private static RabbitMQ.Stream.Client.Message ToRabbitMqMessage(Message message,
        RabbitMqStreamPublication publication,
        AmanhecerContext context)
    {
        var encoding = publication.Encoding;

        var data = new Data(new ReadOnlySequence<byte>(message.Payload));
        var rmqMessage = new RabbitMQ.Stream.Client.Message(data)
        {
            Properties =
            {
                ContentEncoding = message.ContentEncoding,
                ContentType = message.ContentType.ToString(),
                CreationTime = message.Time.UtcDateTime,
                CorrelationId = message.CorrelationId,
                GroupId = message.PartitionKey,
                Subject = message.Subject,
                ReplyTo = message.ReplyTo,
                MessageId = message.Id,
            },
        };

        if (!string.IsNullOrEmpty(publication.UserId))
        {
            rmqMessage.Properties.UserId = encoding.GetBytes(publication.UserId);
        }

        var expireAt = context.GetMetadata<object?>(Metadata.Expiration);
        rmqMessage.Properties.AbsoluteExpiryTime = expireAt switch
        {
            DateTime expireAtTime => expireAtTime,
            DateTimeOffset expireAtOffset => expireAtOffset.UtcDateTime,
            TimeSpan expireAtTimeSpan => DateTime.UtcNow.Add(expireAtTimeSpan),
            _ => rmqMessage.Properties.AbsoluteExpiryTime
        };

        if (message.Metadata.TryGetValue(Metadata.GroupSequence, out var obj) && obj is uint groupSequence)
        {
            rmqMessage.Properties.GroupSequence = groupSequence;
        }

        if (message.Metadata.TryGetValue(Metadata.To, out var objTo) && objTo is string to)
        {
            rmqMessage.Properties.To = to;
        }

        if (message.Metadata.TryGetValue(Metadata.ReplyToGroupId, out var objReplyTo) && objReplyTo is string replyTo)
        {
            rmqMessage.Properties.ReplyToGroupId = replyTo;
        }

        foreach (var (key, val) in message.Headers)
        {
            rmqMessage.ApplicationProperties[key] = val;
        }

        return rmqMessage;
    }
    
    private static void SetCloudEventHeaders(Message message, IPublication publication)
    {
        message.Headers["cloudEvents:id"] = message.Id;
        message.Headers["cloudEvents:source"] = message.Source.ToString();
        message.Headers["cloudEvents:specversion"] = message.SpecVersion;
        message.Headers["cloudEvents:type"] = message.Type;
        message.Headers["cloudEvents:time"] = message.Time.ToString("O", CultureInfo.InvariantCulture);
        message.Headers["cloudEvents:datacontenttype"] = message.ContentType.ToString();

        if (message.DataSchema != null)
        {
            message.Headers["cloudEvents:datacontentschema"] = message.DataSchema.ToString();
        }

        if (message.Subject != null)
        {
            message.Headers["cloudEvents:subject"] = message.Subject;
        }
        
        if (message.Baggage != null)
        {
            message.Headers["cloudEvents:baggage"] = message.Baggage.ToString();
        }
        
        if (message.TraceParent != null)
        {
            message.Headers["cloudEvents:traceparent"] = message.TraceParent;
        }

        if (message.TraceState != null)
        {
            message.Headers["cloudEvents:tracestate"] = message.TraceState.ToString();
        }

        foreach (var additional in publication.AdditionalCloudEvents)
        {
            var key = $"cloudEvents:{additional.Key}";
            if (!message.Headers.ContainsKey(key))
            {
                message.Headers[key] = additional.Value;
            }
        }
    }
}