using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;
using IConsumer = Amanhecer.Abstractions.Messaging.IConsumer;
using Message = Amanhecer.Abstractions.Messaging.Message;

namespace Amanhecer.RabbitMq.Streams;

/// <summary>RabbitMQ Streams implementation of <see cref="IConsumer"/> that reads from a stream via a <see cref="ConsumerConfig"/>.</summary>
public partial class RabbitMqStreamConsumer(
    ConsumerConfig config,
    RabbitMqStreamSubscription subscription,
    ILogger<RabbitMqStreamConsumer>? logger = null) : IConsumer, IAsyncDisposable
{
    private readonly ILogger<RabbitMqStreamConsumer> _logger = logger ?? NullLogger<RabbitMqStreamConsumer>.Instance;
    private Consumer? _consumer;
    private readonly Channel<Message> _channel = Channel.CreateUnbounded<Message>();

    /// <inheritdoc/>
    public ISubscription Subscription => subscription;

    /// <summary>Creates the underlying <see cref="Consumer"/> and starts receiving messages into the internal channel.</summary>
    public async Task InitAsync()
    {
        config.MessageHandler = async (stream, consumer, context, message) =>
        {
            var props = message.Properties;
            var amanhecerMessage = new Message
            {
                Metadata = new()
                {
                    [Metadata.Consumer] = consumer,
                    [Metadata.Context] = context,
                    [Metadata.OriginalMessage] = message,
                    [Metadata.Stream] = stream
                },
                Payload = new ReadOnlyMemory<byte>(message.Data.Contents.ToArray()),
                Id = props.MessageId?.ToString() ?? Uuid.NewGuid().ToString(),
                CorrelationId = props.CorrelationId?.ToString() ?? Uuid.NewGuid().ToString(),
                ContentEncoding = props.ContentEncoding,
                Subject = props.Subject,
                ReplyTo = props.ReplyTo,
                PartitionKey = props.GroupId,
            };

            if (props.ContentType != null)
            {
                amanhecerMessage.ContentType = new ContentType(props.ContentType);
            }

            if (props.CreationTime != default)
            {
                amanhecerMessage.Time = new DateTimeOffset(props.CreationTime, TimeSpan.Zero);
            }

            if (props.AbsoluteExpiryTime != default)
            {
                amanhecerMessage.Metadata[Metadata.Expiration] = props.AbsoluteExpiryTime;
            }

            amanhecerMessage.Metadata[Metadata.GroupSequence] = props.GroupSequence;

            if (props.To != null)
            {
                amanhecerMessage.Metadata[Metadata.To] = props.To;
            }

            if (props.ReplyToGroupId != null)
            {
                amanhecerMessage.Metadata[Metadata.ReplyToGroupId] = props.ReplyToGroupId;
            }

            foreach (var (key, val) in message.ApplicationProperties)
            {
                if (key.StartsWith("cloudEvents:", StringComparison.Ordinal))
                {
                    ApplyCloudEventHeader(amanhecerMessage, key, val);
                }
                else
                {
                    amanhecerMessage.Headers[key] = val;
                }
            }

            await _channel.Writer.WriteAsync(amanhecerMessage);
        };

        _consumer = await Consumer.Create(config);
    }

    /// <inheritdoc/>
    public async ValueTask AckAsync(Message message)
    {
        if (!TryGetConsumerContext(message, out var rawConsumer, out var context))
        {
            return;
        }

        await rawConsumer.StoreOffset(context.Offset);
    }

    /// <inheritdoc/>
    public ValueTask NackAsync(Message message)
    {
        Logger.NackNotSupported(_logger, message.Id, subscription.Stream);
        return AckAsync(message);
    }

    /// <inheritdoc/>
    public ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        Logger.DeferNotSupported(_logger, message.Id, subscription.Stream);
        return AckAsync(message);
    }

    /// <inheritdoc/>
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new List<Message>(subscription.BufferSize);

        var reader = _channel.Reader;
        if (await reader.WaitToReadAsync(cancellationToken))
        {
            while (!cancellationToken.IsCancellationRequested
                   && buffer.Count < subscription.BufferSize
                   && reader.TryRead(out var message))
            {
                buffer.Add(message);
            }
        }

        return [.. buffer];
    }

    private bool TryGetConsumerContext(Message message, out RawConsumer rawConsumer, out MessageContext context)
    {
        if (!message.Metadata.TryGetValue(Metadata.Consumer, out var obj) || obj is not RawConsumer consumer)
        {
            rawConsumer = null!;
            context = default;
            Logger.MissingConsumerMetadata(_logger, message.Id, subscription.Stream);
            return false;
        }

        if (!message.Metadata.TryGetValue(Metadata.Context, out var objContext) || objContext is not MessageContext ctx)
        {
            rawConsumer = null!;
            context = default;
            Logger.MissingContextMetadata(_logger, message.Id, subscription.Stream);
            return false;
        }

        rawConsumer = consumer;
        context = ctx;
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_consumer != null)
        {
            await _consumer.Close();
            _consumer = null;
        }

        _channel.Writer.Complete();
    }

    private static void ApplyCloudEventHeader(Message message, string key, object? value)
    {
        if (value is not string str)
            return;

        switch (key)
        {
            case "cloudEvents:id":
                message.Id = str;
                break;
            case "cloudEvents:source":
                if (Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var source))
                    message.Source = source;
                break;
            case "cloudEvents:specversion":
                message.SpecVersion = str;
                break;
            case "cloudEvents:type":
                message.Type = str;
                break;
            case "cloudEvents:time":
                if (DateTimeOffset.TryParseExact(str, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var time))
                    message.Time = time;
                break;
            case "cloudEvents:datacontenttype":
                message.ContentType = new ContentType(str);
                break;
            case "cloudEvents:datacontentschema":
                if (Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var schema))
                    message.DataSchema = schema;
                break;
            case "cloudEvents:subject":
                message.Subject = str;
                break;
            case "cloudEvents:baggage":
                message.Baggage = Baggage.FromString(str);
                break;
            case "cloudEvents:traceparent":
                message.TraceParent = str;
                break;
            case "cloudEvents:tracestate":
                message.TraceState = TraceState.FromString(str);
                break;
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning,
            "RabbitMQ Streams does not support nack; acknowledging message {MessageId} on stream {Stream} instead")]
        public static partial void NackNotSupported(ILogger logger, string messageId, string stream);

        [LoggerMessage(LogLevel.Warning,
            "RabbitMQ Streams does not support defer; acknowledging message {MessageId} on stream {Stream} instead")]
        public static partial void DeferNotSupported(ILogger logger, string messageId, string stream);

        [LoggerMessage(LogLevel.Warning,
            "Cannot settle message {MessageId} on stream {Stream}: no RawConsumer found in metadata")]
        public static partial void MissingConsumerMetadata(ILogger logger, string messageId, string stream);

        [LoggerMessage(LogLevel.Warning,
            "Cannot settle message {MessageId} on stream {Stream}: no MessageContext found in metadata")]
        public static partial void MissingContextMetadata(ILogger logger, string messageId, string stream);
    }
}