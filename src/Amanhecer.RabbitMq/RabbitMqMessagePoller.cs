using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

/// <summary>
/// An <see cref="AsyncDefaultBasicConsumer"/> that maps RabbitMQ deliveries to
/// <see cref="Message"/>s and buffers them for the message pump. The buffer is bounded by
/// the subscription's <see cref="Subscription.BufferSize"/>, which is also used as the QoS
/// prefetch count, providing end-to-end backpressure.
/// </summary>
public class RabbitMqMessagePoller(
    RabbitMqSubscription subscription,
#if NETFRAMEWORK
    IModel channel
#else
    IChannel channel
#endif
) : AsyncDefaultBasicConsumer(channel)
{
    private readonly Channel<Message> _buffer = System.Threading.Channels.Channel.CreateBounded<Message>(
        new BoundedChannelOptions(subscription.BufferSize)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    // RabbitMQ.Client channels are not thread-safe: every operation on the channel goes
    // through this lock.
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    /// <summary>
    /// Gets the reader over the buffered messages waiting to be pumped through the pipeline.
    /// </summary>
    public ChannelReader<Message> Messages => _buffer.Reader;

#if NETFRAMEWORK
    /// <summary>
    /// Handles a delivered message: maps it to a <see cref="Message"/> and buffers it for the
    /// message pump. A message that cannot be mapped is nacked without requeue (poison message).
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
    /// Handles a delivered message: maps it to a <see cref="Message"/> and buffers it for the
    /// message pump. A message that cannot be mapped is nacked without requeue (poison message).
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
        Message message;
        try
        {
            message = ToMessage(consumerTag, deliveryTag, redelivered, exchange, routingKey, body, properties);
        }
        catch
        {
            // The delivery cannot be mapped to a message: nack without requeue so the poison
            // message is dead-lettered instead of being redelivered forever.
            await NackAsync(deliveryTag, requeue: false);
            return;
        }

        await _buffer.Writer.WriteAsync(message);
    }

    /// <summary>
    /// Acks the delivery identified by <paramref name="deliveryTag"/>.
    /// </summary>
    internal async Task AckAsync(ulong deliveryTag, CancellationToken cancellationToken = default)
    {
        await _channelLock.WaitAsync(cancellationToken);
        try
        {
#if NETFRAMEWORK
            Model.BasicAck(deliveryTag, false);
#else
            await Channel.BasicAckAsync(deliveryTag, false, cancellationToken);
#endif
        }
        finally
        {
            _channelLock.Release();
        }
    }

    /// <summary>
    /// Nacks the delivery identified by <paramref name="deliveryTag"/>.
    /// </summary>
    /// <param name="deliveryTag">The tag identifying the delivery to nack.</param>
    /// <param name="requeue">Whether the broker should requeue the delivery.</param>
    /// <param name="cancellationToken">A token that signals the operation should be aborted.</param>
    internal async Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default)
    {
        await _channelLock.WaitAsync(cancellationToken);
        try
        {
#if NETFRAMEWORK
            Model.BasicNack(deliveryTag, false, requeue);
#else
            await Channel.BasicNackAsync(deliveryTag, false, requeue, cancellationToken);
#endif
        }
        finally
        {
            _channelLock.Release();
        }
    }

    /// <summary>
    /// Completes the message buffer, releasing any pending readers.
    /// </summary>
    internal void Complete()
    {
        _buffer.Writer.TryComplete();
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
            [MetadataName.MessageId] = properties.MessageId,
            [MetadataName.ConsumerTag] = consumerTag,
            [MetadataName.DeliveryTag] = deliveryTag,
            [MetadataName.Redelivered] = redelivered,
            [MetadataName.Exchange] = exchange,
            [MetadataName.RoutingKey] = routingKey
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

    // RabbitMQ.Client delivers AMQP long-string header values as byte[], not string.
    private static string? GetHeaderValue(IDictionary<string, object?> properties, string key)
    {
        if (!properties.TryGetValue(key, out var obj))
        {
            return null;
        }

        return obj switch
        {
            string val => val,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => null
        };
    }

    private static string GetId(IDictionary<string, object?> properties, string? messageId)
    {
        var id = GetHeaderValue(properties, "cloudEvents:id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
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
        return GetHeaderValue(properties, "cloudEvents:dataref");
    }

    private static Uri? GetDataSchema(Dictionary<string, object?> properties)
    {
        var val = GetHeaderValue(properties, "cloudEvents:dataschema");
        return val != null && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : null;
    }

    private static string? GetSubject(Dictionary<string, object?> properties)
    {
        return GetHeaderValue(properties, "cloudEvents:subject");
    }

    private string GetSpecVersion(Dictionary<string, object?> properties)
    {
        return GetHeaderValue(properties, "cloudEvents:specversion") ?? subscription.DefaultSpecVersion;
    }

    private Uri GetSource(Dictionary<string, object?> properties)
    {
        var val = GetHeaderValue(properties, "cloudEvents:source");
        return val != null && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : subscription.DefaultSource;
    }

    private string GetType(Dictionary<string, object?> properties)
    {
        return GetHeaderValue(properties, "cloudEvents:type") ?? subscription.DefaultType;
    }

    private static Baggage? GetBaggage(Dictionary<string, object?> properties)
    {
        var val = GetHeaderValue(properties, "cloudEvents:baggage");
        return val != null ? Baggage.FromString(val) : null;
    }

    private static DateTimeOffset GetTime(AmqpTimestamp timestamp, Dictionary<string, object?> properties)
    {
        var val = GetHeaderValue(properties, "cloudEvents:time");
        if (val != null && DateTimeOffset.TryParse(val, out var time))
        {
            return time;
        }

        // The publisher did not set a timestamp: fall back to now rather than epoch 0.
        return timestamp.UnixTime == 0
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime);
    }

    private static string? GetTraceParent(Dictionary<string, object?> properties)
    {
        return GetHeaderValue(properties, "cloudEvents:traceparent");
    }

    private static TraceState? GetTraceState(Dictionary<string, object?> properties)
    {
        var val = GetHeaderValue(properties, "cloudEvents:tracestate");
        return val != null ? TraceState.FromString(val) : null;
    }
}
