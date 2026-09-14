using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Amanhecer.RabbitMq;

/// <summary>
/// An <see cref="IProducer"/> that publishes messages to a RabbitMQ exchange.
/// </summary>
public partial class RabbitMqProducer : IProducer
    , IDisposable
#if !NETFRAMEWORK
    , IAsyncDisposable
#endif
{
    // RabbitMQ.Client channels are not thread-safe: every operation on the channel goes
    // through this lock.
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly ILogger<RabbitMqProducer> _logger;
#if NETFRAMEWORK
    private readonly IModel _channel;
#else
    private readonly IChannel _channel;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqProducer"/> class.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel used to publish messages.</param>
    /// <param name="logger">The logger used to report messages returned by the broker.</param>
    public RabbitMqProducer(
#if NETFRAMEWORK
        IModel channel,
#else
        IChannel channel,
#endif
        ILogger<RabbitMqProducer>? logger = null)
    {
        _channel = channel;
        _logger = logger ?? NullLogger<RabbitMqProducer>.Instance;

        // Mandatory publishes the broker cannot route come back through basic.return.
#if NETFRAMEWORK
        _channel.BasicReturn += OnBasicReturn;
#else
        _channel.BasicReturnAsync += OnBasicReturnAsync;
#endif
    }

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

    /// <summary>Counts messages returned by the broker as unroutable (mandatory publishes).</summary>
    private static readonly Counter<int> ReturnedCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.publish.returned",
        unit: "{message}",
        description: "Number of published messages returned by the broker as unroutable.");

    /// <summary>Records how long publishing a message took, in seconds.</summary>
    private static readonly Histogram<double> ProducerDuration =
        AmanhecerDiagnostics.Meter.CreateHistogram<double>(
            "amanhecer.message.publish.duration",
            unit: "s",
            description: "Duration of message publishing, in seconds.");

    /// <inheritdoc />
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context)
    {
        if (publication is not RabbitMqPublication rabbitMqPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(RabbitMqPublication)}.",
                nameof(publication));
        }

        var exchange = rabbitMqPublication.Exchange
            ?? throw new InvalidOperationException(
                $"The publication '{rabbitMqPublication.RoutingKey}' has no exchange configured.");

        // Low-cardinality tags shared by the metrics instruments (OTel messaging conventions).
        var metricTags = new List<KeyValuePair<string, object?>>
        {
            new("messaging.system", "rabbitmq"),
            new("messaging.operation.type", "publish"),
            new("messaging.destination.name", exchange.Name),
        }.ToArray();

        // Per-message values are high-cardinality, so they go on the span only, never on metrics.
        // The routing key is span-only too: on topic exchanges it can take unbounded values.
        var spanTags = new List<KeyValuePair<string, object?>>(metricTags)
        {
            new("messaging.rabbitmq.destination.routing_key", publication.RoutingKey),
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

        // Copy the span's trace context onto the message before the headers and properties
        // are built, so the published traceparent identifies this producer span.
        message.Enrich(activity);

        BinaryCloudEventHeaders.Apply(message, publication);

        var duration = Stopwatch.StartNew();
        try
        {
            var properties = CreateProperties(message, context, rabbitMqPublication);

            await _channelLock.WaitAsync(context.CancellationToken);
            try
            {
                await PublishAsync(exchange.Name,
                        rabbitMqPublication.RabbitMqRoutingKey,
                        rabbitMqPublication.Mandatory,
                        properties,
                        message.Payload,
                        context.CancellationToken)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
            finally
            {
                _channelLock.Release();
            }

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

#if NETFRAMEWORK
    private void OnBasicReturn(object? sender, BasicReturnEventArgs args)
    {
        HandleBasicReturn(args.Exchange,
            args.RoutingKey,
            args.ReplyCode,
            args.ReplyText,
            args.BasicProperties?.MessageId);
    }
#else
    private Task OnBasicReturnAsync(object sender, BasicReturnEventArgs args)
    {
        HandleBasicReturn(args.Exchange,
            args.RoutingKey,
            args.ReplyCode,
            args.ReplyText,
            args.BasicProperties.MessageId);
        return Task.CompletedTask;
    }
#endif

    private void HandleBasicReturn(string exchange,
        string routingKey,
        ushort replyCode,
        string replyText,
        string? messageId)
    {
        Logger.MessageReturned(_logger, exchange, routingKey, replyCode, replyText, messageId);
        ReturnedCounter.Add(1,
            new List<KeyValuePair<string, object?>>
            {
                new("messaging.system", "rabbitmq"),
                new("messaging.operation.type", "publish"),
                new("messaging.destination.name", exchange),
            }.ToArray());
    }

#if NETFRAMEWORK
    private ValueTask PublishAsync(
        string exchange,
        string routingKey,
        bool mandatory,
        IBasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        _channel.BasicPublish(exchange, routingKey, mandatory, properties, body);
        return new ValueTask();
    }

    private IBasicProperties CreateProperties(Message message, AmanhecerContext context,
        RabbitMqPublication publication)
    {
        var properties = _channel.CreateBasicProperties();
        properties.MessageId = message.Id;
        properties.CorrelationId = message.CorrelationId;
        properties.ReplyTo = message.ReplyTo;
        properties.Timestamp = new AmqpTimestamp(message.Time.ToUnixTimeSeconds());
        properties.UserId = publication.UserId;
        properties.AppId = publication.AppId;
        properties.ClusterId = publication.ClusterId;
        properties.Headers = message.Headers;
        properties.ContentType = message.ContentType?.ToString() ?? publication.DefaultContentType.ToString();

        var encoding = context.GetMetadata<string?>(MetadataName.ContentEncoding);
        properties.ContentEncoding = encoding ?? publication.ContentEncoding;

        var persistent = context.GetMetadata<bool?>(MetadataName.Persistent);
        properties.Persistent = persistent ?? publication.Persistent;

        var expiration = context.GetMetadata<string?>(MetadataName.Expiration);
        properties.Expiration = expiration;

        var priority = context.GetMetadata<byte?>(MetadataName.Priority);
        if (priority != null)
        {
            properties.Priority = priority.Value;
        }

        return properties;
    }
#else
    private static BasicProperties CreateProperties(Message message, AmanhecerContext context,
        RabbitMqPublication publication)
    {
        var properties = new BasicProperties
        {
            MessageId = message.Id,
            CorrelationId = message.CorrelationId,
            ReplyTo = message.ReplyTo,
            Timestamp = new AmqpTimestamp(message.Time.ToUnixTimeSeconds()),
            UserId = publication.UserId,
            AppId = publication.AppId,
            ClusterId = publication.ClusterId,
            Headers = message.Headers!,
            ContentType = message.ContentType?.ToString() ?? publication.DefaultContentType.ToString()
        };

        var encoding = context.GetMetadata<string?>(MetadataName.ContentEncoding);
        properties.ContentEncoding = encoding ?? publication.ContentEncoding;

        var persistent = context.GetMetadata<bool?>(MetadataName.Persistent);
        properties.Persistent = persistent ?? publication.Persistent;

        var expiration = context.GetMetadata<string?>(MetadataName.Expiration);
        properties.Expiration = expiration;

        var priority = context.GetMetadata<byte?>(MetadataName.Priority);
        if (priority != null)
        {
            properties.Priority = priority.Value;
        }

        return properties;
    }

    private async ValueTask PublishAsync(
        string exchange,
        string routingKey,
        bool mandatory,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        await _channel.BasicPublishAsync(exchange, routingKey, mandatory, properties, body, cancellationToken);
    }
#endif


    /// <inheritdoc />
    public void Dispose()
    {
        _channel.Dispose();
        _channelLock.Dispose();
    }

#if !NETFRAMEWORK
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        _channelLock.Dispose();
    }
#endif

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning,
            "Message {MessageId} published to exchange {Exchange} with routing key {RoutingKey} was returned by the broker as unroutable ({ReplyCode} {ReplyText})")]
        public static partial void MessageReturned(ILogger logger,
            string exchange,
            string routingKey,
            ushort replyCode,
            string replyText,
            string? messageId);
    }
}