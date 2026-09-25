using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Compression;
using Amanhecer.Messaging.Transformers;
using Amanhecer.RabbitMq.Provisioners;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures a RabbitMQ publication: the exchange and routing key messages are published
/// with, the message mapper used to build them, and the default message attributes applied
/// to them.
/// </summary>
public class RabbitMqPublicationConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the publication. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The publication name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Name(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Publication name cannot be null or empty.", nameof(name));
        }

        _name = name;
        return this;
    }

    private string? _routingKey;

    /// <summary>
    /// Sets the routing key used to resolve this publication when a message is sent.
    /// </summary>
    /// <param name="routingKey">The publication routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator RoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _routingKey = routingKey;
        return this;
    }

    private string? _rabbitMqRoutingKey;

    /// <summary>
    /// Sets the RabbitMQ routing key messages are published to the exchange with. An empty
    /// routing key is allowed only when the exchange is declared as <c>fanout</c> (see
    /// <see cref="RabbitMqExchangeConfigurator.CreateIfNotExistsConfigurator.Type"/>), which
    /// routes messages to every bound queue regardless of the routing key.
    /// </summary>
    /// <param name="routingKey">The RabbitMQ routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator RabbitMqRoutingKey(string routingKey)
    {
        _rabbitMqRoutingKey = routingKey;
        return this;
    }

    private Exchange? _exchange;


    /// <summary>
    /// Sets the exchange messages published through this publication are sent to.
    /// </summary>
    /// <param name="exchange">The exchange instance.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Exchange(Exchange exchange)
    {
        _exchange = exchange;
        return this;
    }

    /// <summary>
    /// Sets the exchange messages published through this publication are sent to.
    /// </summary>
    /// <param name="configure">A delegate that configures the exchange.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Exchange(Action<RabbitMqExchangeConfigurator> configure)
    {
        var cfg = new RabbitMqExchangeConfigurator();
        configure.Invoke(cfg);

        _exchange = cfg.ToExchange();
        return this;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _messageMapperType;


    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between application
    /// requests and the messages published through this publication.
    /// </summary>
    /// <param name="mapper">The message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator MessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type mapper)
    {
        _messageMapperType = mapper;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between application
    /// requests and the messages published through this publication.
    /// </summary>
    /// <typeparam name="TMapper">The message mapper implementation type.</typeparam>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private readonly List<AmanhecerTransformerOptions> _transformers = [];

    /// <summary>
    /// Adds a transformer to the encode pipeline messages published through this publication
    /// go through, on top of any globally registered transformers.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer implementation type.</typeparam>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
        where TTransformer : IEncodeTransformer
    {
        return Transformer(typeof(TTransformer), order, metadata);
    }

    /// <summary>
    /// Adds a transformer to the encode pipeline messages published through this publication
    /// go through, on top of any globally registered transformers.
    /// </summary>
    /// <param name="transformerType">The transformer implementation type.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
    {
        if (!typeof(IEncodeTransformer).IsAssignableFrom(transformerType))
        {
            throw new ArgumentException(
                $"The type '{transformerType.FullName}' does not implement IEncodeTransformer.",
                nameof(transformerType));
        }

        _transformers.Add(new AmanhecerTransformerOptions(transformerType, order, metadata));
        return this;
    }

    private bool _mandatory;

    /// <summary>
    /// Sets whether messages are published with the RabbitMQ <c>mandatory</c> flag, causing
    /// the broker to return messages that cannot be routed to any queue.
    /// </summary>
    /// <param name="mandatory">Whether the mandatory flag is set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Mandatory(bool mandatory = true)
    {
        _mandatory = mandatory;
        return this;
    }

    private bool _persistent;

    /// <summary>
    /// Sets whether messages are published as persistent, so the broker writes them to disk.
    /// </summary>
    /// <param name="persistent">Whether messages are persistent.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator Persistent(bool persistent = true)
    {
        _persistent = persistent;
        return this;
    }

    private string _contentEncoding = "utf-8";

    /// <summary>
    /// Sets the content encoding declared on published messages.
    /// </summary>
    /// <param name="contentEncoding">The content encoding. Defaults to <c>utf-8</c>.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator ContentEncoding(string contentEncoding)
    {
        if (string.IsNullOrEmpty(contentEncoding))
        {
            throw new ArgumentException("Content encoding cannot be null or empty.", nameof(contentEncoding));
        }

        _contentEncoding = contentEncoding;
        return this;
    }

    private string? _userId;

    /// <summary>
    /// Sets the AMQP <c>user-id</c> property set on published messages.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator UserId(string userId)
    {
        _userId = userId;
        return this;
    }

    private string? _appId;

    /// <summary>
    /// Sets the AMQP <c>app-id</c> property set on published messages.
    /// </summary>
    /// <param name="appId">The application id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator AppId(string appId)
    {
        _appId = appId;
        return this;
    }

    private string? _clusterId;

    /// <summary>
    /// Sets the AMQP <c>cluster-id</c> property set on published messages.
    /// </summary>
    /// <param name="clusterId">The cluster id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator ClusterId(string clusterId)
    {
        _clusterId = clusterId;
        return this;
    }

    private ContentType? _defaultContentType;

    /// <summary>
    /// Sets the content type set on messages that do not specify one.
    /// </summary>
    /// <param name="contentType">The default content type. Defaults to <c>text/plain</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            throw new ArgumentException("Content type cannot be null or empty.", nameof(contentType));
        }

        _defaultContentType = new ContentType(contentType);
        return this;
    }

    private Uri? _defaultDataSchema;

    /// <summary>
    /// Sets the schema the message payload conforms to (the CloudEvents <c>dataschema</c>
    /// attribute), set on messages that do not specify one.
    /// </summary>
    /// <param name="dataSchema">The default data schema URI.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultDataSchema(Uri dataSchema)
    {
        _defaultDataSchema = dataSchema;
        return this;
    }

    private Uri _defaultSource = Message.DefaultSource;

    /// <summary>
    /// Sets the source (the CloudEvents <c>source</c> attribute) set on messages that do not
    /// specify one.
    /// </summary>
    /// <param name="source">The default source URI.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultSource(Uri source)
    {
        _defaultSource = source;
        return this;
    }

    private string? _defaultSubject;

    /// <summary>
    /// Sets the subject (the CloudEvents <c>subject</c> attribute) set on messages that do
    /// not specify one.
    /// </summary>
    /// <param name="subject">The default subject.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultSubject(string subject)
    {
        _defaultSubject = subject;
        return this;
    }

    private string _defaultType = Message.DefaultType;

    /// <summary>
    /// Sets the type (the CloudEvents <c>type</c> attribute) set on messages that do not
    /// specify one.
    /// </summary>
    /// <param name="type">The default type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private string? _defaultReplyTo;

    /// <summary>
    /// Sets the address replies to messages published through this publication should be
    /// sent to, when the message does not specify one.
    /// </summary>
    /// <param name="replyTo">The default reply-to address.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultReplyTo(string replyTo)
    {
        _defaultReplyTo = replyTo;
        return this;
    }

    private readonly Dictionary<string, object> _defaultHeaders = [];

    /// <summary>
    /// Adds a header set on every message published through this publication, unless the
    /// message already carries the same header.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator DefaultHeader(string key, object value)
    {
        _defaultHeaders[key] = value;
        return this;
    }

    private readonly Dictionary<string, object> _additionalCloudEvents = [];

    /// <summary>
    /// Adds an additional CloudEvents attribute (extension attribute) set on every message
    /// published through this publication.
    /// </summary>
    /// <param name="key">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator AdditionalCloudEvent(string key, object value)
    {
        _additionalCloudEvents[key] = value;
        return this;
    }

    private CloudEventType? _cloudEventType;

    /// <summary>
    /// Sets the CloudEvents content mode used to encode messages published through this
    /// publication.
    /// </summary>
    /// <param name="cloudEventType">The content mode. Defaults to binary content mode when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationConfigurator CloudEventType(CloudEventType cloudEventType)
    {
        _cloudEventType = cloudEventType;
        return this;
    }

    internal RabbitMqPublication ToPublication()
    {
        if (string.IsNullOrEmpty(_routingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a publication. Call RoutingKey to configure it.");
        }

        if (_rabbitMqRoutingKey is null)
        {
            throw new InvalidOperationException(
                "A RabbitMQ routing key is required for a publication. Call RabbitMqRoutingKey to configure it.");
        }

        if (_exchange is null)
        {
            throw new InvalidOperationException(
                "An exchange is required for a publication. Call Exchange to configure it.");
        }

        if (_rabbitMqRoutingKey.Length == 0 && !IsFanout(_exchange))
        {
            throw new InvalidOperationException(
                "An empty RabbitMQ routing key is only valid when the exchange is declared as fanout. " +
                "Call RabbitMqRoutingKey with a non-empty routing key, or declare the exchange with " +
                "CreateIfNotExists and Type(\"fanout\").");
        }

        var transformers = _transformers;
        if (_cloudEventType == Abstractions.Messaging.CloudEventType.Json)
        {
            // The envelope wrap runs last, once the attributes and defaults are set.
            transformers =
            [
                .. _transformers,
                new AmanhecerTransformerOptions(typeof(StructuredCloudEventTransformer), int.MaxValue, null)
            ];
        }

        return new RabbitMqPublication
        {
            RoutingKey = _routingKey!,
            RabbitMqRoutingKey = _rabbitMqRoutingKey!,
            Exchange = _exchange,
            MessageMapperType = _messageMapperType,
            Transformers = [.. transformers.OrderBy(x => x.Order)],
            Mandatory = _mandatory,
            Persistent = _persistent,
            ContentEncoding = _contentEncoding,
            UserId = _userId,
            AppId = _appId,
            ClusterId = _clusterId,
            DefaultHeaders = _defaultHeaders,
            AdditionalCloudEvents = _additionalCloudEvents,
            DefaultDataSchema = _defaultDataSchema,
            DefaultSubject = _defaultSubject,
            DefaultType = _defaultType,
            DefaultReplyTo = _defaultReplyTo,
            CloudEventType = _cloudEventType ?? Abstractions.Messaging.CloudEventType.Binary,
            Name = _name ?? Uuid.NewGuid().ToString(),
            DefaultContentType = _defaultContentType ?? new ContentType("text/plain"),
            DefaultSource = _defaultSource
        };
    }

    private static bool IsFanout(Exchange exchange)
    {
        return exchange.Provisioner is CreateIfNotExchange provisioner
            && string.Equals(provisioner.Type, ExchangeType.Fanout, StringComparison.OrdinalIgnoreCase);
    }
}
