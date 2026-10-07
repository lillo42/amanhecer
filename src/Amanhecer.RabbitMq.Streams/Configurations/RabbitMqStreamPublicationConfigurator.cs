using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures a RabbitMQ Streams publication: the stream messages are published to, the
/// routing key used to resolve this publication, and the message mapper used to build them.
/// </summary>
public class RabbitMqStreamPublicationConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the publication. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The publication name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Name(string name)
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
    public RabbitMqStreamPublicationConfigurator RoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _routingKey = routingKey;
        return this;
    }

    private string? _stream;

    /// <summary>
    /// Sets the name of the RabbitMQ stream messages are published to.
    /// </summary>
    /// <param name="stream">The stream name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Stream(string stream)
    {
        if (string.IsNullOrEmpty(stream))
        {
            throw new ArgumentException("Stream name cannot be null or empty.", nameof(stream));
        }

        _stream = stream;
        return this;
    }

    private string? _userId;

    /// <summary>
    /// Sets the user id applied to published messages.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator UserId(string userId)
    {
        _userId = userId;
        return this;
    }

    private Encoding? _encoding;

    /// <summary>
    /// Sets the encoding used to serialize string values such as the user id. Defaults to UTF-8.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Encoding(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        return this;
    }

    private Action<ProducerConfig>? _configure;

    /// <summary>
    /// Registers a callback to further configure the <see cref="ProducerConfig"/> before the
    /// producer is created.
    /// </summary>
    /// <param name="configure">A delegate that receives the <see cref="ProducerConfig"/> being configured.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Configure(Action<ProducerConfig> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
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
    public RabbitMqStreamPublicationConfigurator MessageMapper(
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
    public RabbitMqStreamPublicationConfigurator MessageMapper<
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
    public RabbitMqStreamPublicationConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
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
    public RabbitMqStreamPublicationConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
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

    /// <summary>
    /// Adds an inline transformer to the encode pipeline messages published through this
    /// publication go through, on top of any globally registered transformers. The delegate is
    /// wrapped in an <see cref="AnonymousEncodeTransformer"/>.
    /// </summary>
    /// <param name="func">The delegate executed as the transformer body.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Transformer(
        Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func,
        int order = 0)
    {
        _transformers.Add(new AmanhecerTransformerOptions(typeof(AnonymousEncodeTransformer), order, func));
        return this;
    }

    private ContentType? _defaultContentType;

    /// <summary>
    /// Sets the content type set on messages that do not specify one.
    /// </summary>
    /// <param name="contentType">The default content type. Defaults to <c>text/plain</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator DefaultContentType(string contentType)
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
    public RabbitMqStreamPublicationConfigurator DefaultDataSchema(Uri dataSchema)
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
    public RabbitMqStreamPublicationConfigurator DefaultSource(Uri source)
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
    public RabbitMqStreamPublicationConfigurator DefaultSubject(string subject)
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
    public RabbitMqStreamPublicationConfigurator DefaultType(string type)
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
    public RabbitMqStreamPublicationConfigurator DefaultReplyTo(string replyTo)
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
    public RabbitMqStreamPublicationConfigurator DefaultHeader(string key, object value)
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
    public RabbitMqStreamPublicationConfigurator AdditionalCloudEvent(string key, object value)
    {
        _additionalCloudEvents[key] = value;
        return this;
    }

    private IPublicationProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources this publication needs before
    /// messages can be published through it.
    /// </summary>
    /// <param name="provisioner">The publication provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator Provisioner(IPublicationProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the stream already exists on the broker and performs no provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator AssumeExists()
    {
        _provisioner = new AssumeStreamExists();
        return this;
    }

    /// <summary>
    /// Validates that the stream exists on the broker, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateStreamExists();
        return this;
    }

    /// <summary>
    /// Creates the stream on the broker when it does not already exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the stream is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator CreateIfNotExists(Action<CreateStreamConfigurator> configure)
    {
        var cfg = new CreateStreamConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
        return this;
    }

    /// <summary>
    /// Creates the stream on the broker when it does not already exist, using default settings.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationConfigurator CreateIfNotExists()
    {
        _provisioner = new CreateStream();
        return this;
    }

    internal RabbitMqStreamPublication ToPublication()
    {
        if (string.IsNullOrEmpty(_routingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a publication. Call RoutingKey to configure it.");
        }

        if (string.IsNullOrEmpty(_stream))
        {
            throw new InvalidOperationException(
                "A stream name is required for a publication. Call Stream to configure it.");
        }

        return new RabbitMqStreamPublication(_stream!)
        {
            RoutingKey = _routingKey!,
            Name = _name ?? Uuid.NewGuid().ToString(),
            MessageMapperType = _messageMapperType,
            Transformers = [.. _transformers],
            UserId = _userId,
            Encoding = _encoding ?? System.Text.Encoding.UTF8,
            Configure = _configure,
            DefaultContentType = _defaultContentType ?? new ContentType("text/plain"),
            DefaultDataSchema = _defaultDataSchema,
            DefaultSource = _defaultSource,
            DefaultSubject = _defaultSubject,
            DefaultType = _defaultType,
            DefaultReplyTo = _defaultReplyTo,
            DefaultHeaders = _defaultHeaders,
            AdditionalCloudEvents = _additionalCloudEvents,
            Provisioner = _provisioner
        };
    }
}
