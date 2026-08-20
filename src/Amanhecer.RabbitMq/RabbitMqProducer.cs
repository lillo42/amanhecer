using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq;

public class RabbitMqProducer(
#if NETFRAMEWORK
    IModel channel
#else
    IChannel channel
#endif
) : IProducer
#if NETFRAMEWORK
    , IDisposable
#endif
{
    public async ValueTask ProducerAsync(Message message, IPublication publication, IPipelineContext context)
    {
        if (publication is not RabbitMqPublication rabbitMqPublication)
        {
            throw new ArgumentException();
        }
        

        SetCloudEventHeaders(message);
        var properties = CreateProperties(message, context, rabbitMqPublication);
        
        await PublishAsync(rabbitMqPublication.Exchange!.Name,
                rabbitMqPublication.RabbitMqRoutingKey,
                rabbitMqPublication.Mandatory,
                properties,
                message.Payload,
                context.CancellationToken)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private static void SetCloudEventHeaders(Message message)
    {
        Set(message, "cloudEvents:id", message.Id);
        Set(message, "cloudEvents:source", message.Source);
        Set(message, "cloudEvents:specversion", message.SpecVersion);
        Set(message, "cloudEvents:type", message.Type);
        Set(message, "cloudEvents:datacontenttype", message.ContentType?.ToString());
        Set(message, "cloudEvents:dataschema", message.DataSchema);
        Set(message, "cloudEvents:subject", message.Subject);
        Set(message, "cloudEvents:time", message.Time.ToString("O"));
        Set(message, "cloudEvents:baggage", message.Baggage?.ToString());
        Set(message, "cloudEvents:traceparent", message.TraceParent);
        Set(message, "cloudEvents:tracestate", message.TraceState?.ToString());
        return;

        static void Set(Message message, string key, object? value)
        {
            if (value == null)
            {
                return;
            }

            var headers = message.Headers;
            if (!headers.ContainsKey(key))
            {
                headers.Add(key, value);
            }
        }
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
        channel.BasicPublish(exchange, routingKey, mandatory, properties, body);
        return new ValueTask();
    }

    private IBasicProperties CreateProperties(Message message, IPipelineContext context,
        RabbitMqPublication publication)
    {
        var properties = channel.CreateBasicProperties();
        properties.MessageId = message.Id;
        properties.CorrelationId = message.CorrelationId;
        properties.ReplyTo = message.ReplyTo;
        properties.Timestamp = new AmqpTimestamp(message.Time.ToUnixTimeSeconds());
        properties.UserId = publication.UserId;
        properties.AppId = publication.AppId;
        properties.ClusterId = publication.ClusterId;
        properties.Headers = message.Headers;

        properties.ContentType = message.ContentType?.ToString() ?? publication.DefaultContentType.ToString();

        var encoding = context.Metadata.GetOrDefault<string?>(MetadataName.ContentEncoding);
        properties.ContentEncoding = encoding ?? publication.ContentEncoding;

        var persistent = context.Metadata.GetOrDefault<bool?>(MetadataName.Persistent);
        properties.Persistent = persistent ?? publication.Persistent;

        var expiration = context.Metadata.GetOrDefault<string?>(MetadataName.Expiration);
        properties.Expiration = expiration;

        var priority = context.Metadata.GetOrDefault<byte?>(MetadataName.Priority);
        if (priority != null)
        {
            properties.Priority = priority.Value;
        }

        return properties;
    }
#else
    private BasicProperties CreateProperties(Message message, IPipelineContext context, RabbitMqPublication publication)
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
        };

        properties.ContentType = message.ContentType?.ToString() ?? publication.DefaultContentType.ToString();

        var encoding = context.Metadata.GetOrDefault<string?>(MetadataName.ContentEncoding);
        properties.ContentEncoding = encoding ?? publication.ContentEncoding;

        var persistent = context.Metadata.GetOrDefault<bool?>(MetadataName.Persistent);
        properties.Persistent = persistent ?? publication.Persistent;

        var expiration = context.Metadata.GetOrDefault<string?>(MetadataName.Expiration);
        properties.Expiration = expiration;

        var priority = context.Metadata.GetOrDefault<byte?>(MetadataName.Priority);
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
        await channel.BasicPublishAsync(exchange, routingKey, mandatory, properties, body, cancellationToken);
    }
#endif


#if NETFRAMEWORK
    public void Dispose()
    {
        channel.Dispose();
    }
#endif
}