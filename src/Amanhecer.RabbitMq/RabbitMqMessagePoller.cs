using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

public class RabbitMqMessagePoller(
    RabbitMqSubscription subscription,
#if NETFRAMEWORK
    IModel channel
#else
    IChannel channel
#endif
) : AsyncDefaultBasicConsumer(channel)
{
    private readonly Channel<Message> _buffer = System.Threading.Channels.Channel.CreateUnbounded<Message>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });

    public ChannelReader<Message> Messages => _buffer.Reader;

#if NETFRAMEWORK
    /// <summary>
    /// Handles a delivered message: maps it to a <see cref="Message"/>, dispatches it through
    /// the pipeline, and acks or nacks the delivery according to the response.
    /// </summary>
    /// <param name="consumerTag">The tag identifying this consumer.</param>
    /// <param name="deliveryTag">The tag identifying the delivery to acknowledge.</param>
    /// <param name="redelivered">Whether the message has been delivered before.</param>
    /// <param name="exchange">The exchange the message was published to.</param>
    /// <param name="routingKey">The routing key the message was published with.</param>
    /// <param name="properties">The properties of the delivered message.</param>
    /// <param name="body">The body of the delivered message.</param>
    /// <returns>A <see cref="Task"/> that completes when the delivery has been handled.</returns>
    public override async Task HandleBasicDeliver(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        IBasicProperties properties, ReadOnlyMemory<byte> body)
#else
    /// <summary>
    /// Handles a delivered message: maps it to a <see cref="Message"/>, dispatches it through
    /// the pipeline, and acks or nacks the delivery according to the response.
    /// </summary>
    /// <param name="consumerTag">The tag identifying this consumer.</param>
    /// <param name="deliveryTag">The tag identifying the delivery to acknowledge.</param>
    /// <param name="redelivered">Whether the message has been delivered before.</param>
    /// <param name="exchange">The exchange the message was published to.</param>
    /// <param name="routingKey">The routing key the message was published with.</param>
    /// <param name="properties">The properties of the delivered message.</param>
    /// <param name="body">The body of the delivered message.</param>
    /// <param name="cancellationToken">A token that signals the handling should be aborted.</param>
    /// <returns>A <see cref="Task"/> that completes when the delivery has been handled.</returns>
    public override async Task HandleBasicDeliverAsync(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
#endif
    {
        var message = ToMessage(consumerTag, deliveryTag, redelivered, exchange, routingKey, body, properties);
        await _buffer.Writer.WriteAsync(message);

        /*    if (_serviceProvider == null)
            {
                throw new NotImplementedException();
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

    #if NETFRAMEWORK
            var cancellationToken = CancellationToken.None;
    #endif
            // Low-cardinality tags shared by the metrics instruments (OTel messaging conventions).
            // The routing key is span-only: on topic exchanges it can take unbounded values.
            var metricTags = new List<KeyValuePair<string, object?>>
            {
                new("messaging.system", "rabbitmq"),
                new("messaging.operation.type", "process"),
                new("messaging.destination.name", subscription.QueueName),
            }.ToArray();


            Message? message = null;
            Activity? activity = null;
            var duration = Stopwatch.StartNew();
            try
            {
                message = ToMessage(consumerTag, deliveryTag, redelivered, exchange, routingKey, body, properties);

                // Per-message values are high-cardinality, so they go on the span only, never on metrics.
                var spanTags = new List<KeyValuePair<string, object?>>(metricTags)
                {
                    new("messaging.rabbitmq.destination.routing_key", routingKey),
                    new("messaging.message.id", message.Id),
                    new("messaging.message.conversation_id", message.CorrelationId),
                    new("cloudevents.event_id", message.Id),
                    new("cloudevents.event_source", message.Source?.ToString()),
                    new("cloudevents.event_spec_version", message.SpecVersion),
                    new("cloudevents.event_type", message.Type),
                }.ToArray();

                // The trace parent must be passed at start time: setting it after the activity
                // has started has no effect and the span would begin a new trace.
                ActivityContext parentContext = default;
                if (ActivityContext.TryParse(message.TraceParent, message.TraceState?.ToString(), out var parsedContext))
                {
                    parentContext = parsedContext;
                }

                activity = AmanhecerDiagnostics.ActivitySource.StartActivity(
                    ActivityKind.Consumer,
                    name: $"{subscription.QueueName} process",
                    parentContext: parentContext,
                    tags: spanTags);

                // Copies the message baggage onto the span.
                activity?.Enrich(message);

                var resp = await dispatcher.QueryAsync(
                    message,
                    new AmanhecerContext
                    {
                        RoutingKey = "Amanhecer.External.Message",
                        Activity = activity,
                        Metadata = new Dictionary<string, object>
                        {
                            [Amanhecer.Abstractions.MetadataName.MessagingGateway] = "RabbitMQ",
                            [Amanhecer.Abstractions.MetadataName.Subscription] = subscription,
                        }
                    },
                    cancellationToken);

                if (resp is Nack)
                {
                    await NackAsync(deliveryTag);

                    duration.Stop();

                    FailedCounter.Add(1, metricTags);
                    activity?.SetStatus(ActivityStatusCode.Error, "Message negatively acknowledged");
                }
                else if (resp is Defer)
                {
                    await DeferAsync(deliveryTag);
                    duration.Stop();

                    FailedCounter.Add(1, metricTags);
                    activity?.SetStatus(ActivityStatusCode.Error, "Message negatively acknowledged");
                }
                else
                {
                    if (resp is Ack or null)
                    {
                        await AckAsync(deliveryTag);
                    }

                    duration.Stop();

                    SuccessCounter.Add(1, metricTags);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (NackException)
            {
                await NackAsync(deliveryTag);

                duration.Stop();

                FailedCounter.Add(1, metricTags);
                activity?.SetStatus(ActivityStatusCode.Error, "Message negatively acknowledged");
            }
            catch (DeferException)
            {
                await DeferAsync(deliveryTag);

                duration.Stop();

                FailedCounter.Add(1, metricTags);
                activity?.SetStatus(ActivityStatusCode.Error, "Message negatively acknowledged");
            }
            catch (Exception ex)
            {
                await DeferAsync(deliveryTag);

                duration.Stop();
                FailedCounter.Add(1, metricTags);

    #if !NET8_0
                activity?.AddException(ex);
    #endif
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            finally
            {
                ConsumerDuration.Record(duration.Elapsed.TotalSeconds, metricTags);
                activity?.Stop();
            }
     */
    }


    private Message ToMessage(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
#if NETFRAMEWORK
        IBasicProperties properties
#else
        IReadOnlyBasicProperties properties
#endif
    )
    {
        var headers = properties.Headers == null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(properties.Headers);

        var metadata = new Dictionary<string, object?>
        {
            ["MessageId"] = properties.MessageId,
            ["ConsumerTag"] = consumerTag,
            ["DeliveryTag"] = deliveryTag,
            ["Redelivered"] = redelivered,
            ["Exchange"] = exchange,
            ["RoutingKey"] = routingKey
        };

        return new Message
        {
            Id = GetId(headers, properties.MessageId),
            ContentType = GetContentType(properties.ContentType),
            CorrelationId = properties.CorrelationId ?? Uuid.NewGuid().ToString(),
            DataRef = GetDataRef(headers),
            DataSchema = GetDataSchema(headers),
            Headers = headers,
            Metadata = metadata,
            Payload = body.ToArray(),
            ReplyTo = properties.ReplyTo,
            Subject = GetSubject(headers),
            SpecVersion = GetSpecVersion(headers),
            Source = GetSource(headers),
            Time = GetTime(properties.Timestamp, headers),
            Type = GetType(headers),
            Baggage = GetBaggage(headers),
            TraceParent = GetTraceParent(headers),
            TraceState = GetTraceState(headers),
        };
    }

    private static string GetId(IDictionary<string, object?> properties, string? messageId)
    {
        if (properties.TryGetValue("cloudEvents:id", out var obj) && obj is string val)
        {
            return val;
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            return messageId!;
        }

        return Uuid.NewGuid().ToString();
    }

    private static ContentType GetContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return new ContentType("text/plain");
        }

        return new ContentType(contentType);
    }

    private static string? GetDataRef(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:dataref", out var obj) && obj is string val)
        {
            return val;
        }

        return null;
    }

    private static Uri? GetDataSchema(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:dataschema", out var obj)
            && obj is string val
            && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri))
        {
            return uri;
        }

        return null;
    }

    private static string? GetSubject(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:subject", out var obj) && obj is string val)
        {
            return val;
        }

        return null;
    }

    private string GetSpecVersion(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:specversion", out var obj) && obj is string val)
        {
            return val;
        }

        return subscription.DefaultSpecVersion;
    }

    private Uri GetSource(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:source", out var obj)
            && obj is string val
            && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri))
        {
            return uri;
        }

        return subscription.DefaultSource;
    }

    private string GetType(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:type", out var obj) && obj is string val)
        {
            return val;
        }

        return subscription.DefaultType;
    }

    private static Baggage? GetBaggage(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:baggage", out var obj) && obj is string val)
        {
            return Baggage.FromString(val);
        }

        return null;
    }

    private static DateTimeOffset GetTime(AmqpTimestamp timestamp, Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:time", out var obj)
            && obj is string val
            && DateTimeOffset.TryParse(val, out var time))
        {
            return time;
        }

        return DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime);
    }

    private static string? GetTraceParent(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:traceparent", out var obj) && obj is string val)
        {
            return val;
        }

        return null;
    }

    private static TraceState? GetTraceState(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:tracestate", out var obj) && obj is string val)
        {
            return TraceState.FromString(val);
        }

        return null;
    }
}