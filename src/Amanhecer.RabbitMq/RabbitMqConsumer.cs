using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

public class RabbitMqConsumer(
    RabbitMqSubscription subscription,
#if NETFRAMEWORK
    IModel channel
#else
    IChannel channel
#endif
) : AsyncDefaultBasicConsumer(channel), IConsumer
{
    private IServiceProvider? _serviceProvider;
    private string? _consumerTag;

#if NETFRAMEWORK
    public override async Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered,
        string exchange,
        string routingKey,
        IBasicProperties properties, ReadOnlyMemory<byte> body)
#else
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
        if (_serviceProvider == null)
        {
            throw new NotImplementedException();
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

#if NETFRAMEWORK
        var cancellationToken = CancellationToken.None;
#endif
        try
        {
            var resp = await dispatcher.QueryAsync(
                ToMessage(consumerTag, deliveryTag, redelivered, exchange, routingKey, body, properties),
                new AmanhecerContext
                {
                    RoutingKey = "Amanhecer.External.Message",
                    Metadata = new Dictionary<string, object>
                    {
                        [Amanhecer.Abstractions.MetadataName.RoutingTo] = subscription.RoutingKey,
                        [Amanhecer.Abstractions.MetadataName.MessagingGateway] = "RabbitMQ",
                    }
                },
                cancellationToken);

            if (resp is Ack or null)
            {
#if NETFRAMEWORK
                Model.BasicAck(deliveryTag, false);
#else
                await Channel.BasicAckAsync(deliveryTag, false, cancellationToken);
#endif
            }
            else if (resp is Nack nack)
            {
#if NETFRAMEWORK
                Model.BasicNack(deliveryTag, false, nack.Requeue);
#else
                await Channel.BasicNackAsync(deliveryTag, false, nack.Requeue, cancellationToken);
#endif
            }
        }
        catch (NackException ex)
        {
#if NETFRAMEWORK
            Model.BasicNack(deliveryTag, false, ex.Requeue);
#else
            await Channel.BasicNackAsync(deliveryTag, false, ex.Requeue, cancellationToken);
#endif
        }
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

        headers["MessageId"] = properties.MessageId;
        headers["ConsumerTag"] = consumerTag;
        headers["DeliveryTag"] = deliveryTag;
        headers["Redelivered"] = redelivered;
        headers["Exchange"] = exchange;
        headers["RoutingKey"] = routingKey;

        return new Message
        {
            Id = GetId(headers, properties.MessageId),
            ContentType = GetContentType(properties.ContentType),
            CorrelationId = properties.CorrelationId ?? Uuid.NewGuid().ToString(),
            DataRef = GetDataRef(headers),
            DataSchema = GetDataSchema(headers),
            Headers = headers,
            Payload = body,
            ReplyTo = properties.ReplyTo,
            Subject = GetSubject(headers),
            SpecVersion = GetSpecVersion(headers),
            Source = GetSource(headers),
            Time = DateTimeOffset.FromUnixTimeMilliseconds(properties.Timestamp.UnixTime),
            Type = GetType(headers),
            Baggage = GeBaggage(headers),
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

    private static Baggage? GeBaggage(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("cloudEvents:baggage", out var obj) && obj is string val)
        {
            return Baggage.FromString(val);
        }

        return null;
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

    public async ValueTask StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_consumerTag))
        {
            return;
        }

        _consumerTag =
#if NETFRAMEWORK
            Model.BasicConsume(subscription.QueueName, false, this);
#else
            await Channel.BasicConsumeAsync(subscription.QueueName, false, this, cancellationToken: cancellationToken);
#endif
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_consumerTag))
        {
            return;
        }

#if NETFRAMEWORK
        Model.BasicCancel(subscription.QueueName);
#else
        await Channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
#endif
    }
}