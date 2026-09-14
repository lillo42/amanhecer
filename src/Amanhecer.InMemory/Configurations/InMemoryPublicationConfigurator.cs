using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;
using Amanhecer.Messaging.Transformers;

namespace Amanhecer.InMemory.Configurations;

/// <summary>
/// Configures an in-memory publication: the routing key used to resolve it, the target queue,
/// the message mapper, and default message attributes.
/// </summary>
public class InMemoryPublicationConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the publication. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The publication name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator Name(string name)
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
    public InMemoryPublicationConfigurator RoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _routingKey = routingKey;
        return this;
    }

    private string? _queueName;

    /// <summary>
    /// Sets the in-memory queue name this publication writes to.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator QueueName(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _queueName = queueName;
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
    public InMemoryPublicationConfigurator MessageMapper(
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
    public InMemoryPublicationConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private readonly List<AmanhecerTransformerOptions> _transformers = [];

    /// <summary>
    /// Adds a transformer to the encode pipeline messages published through this publication go
    /// through, on top of any globally registered transformers.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer implementation type.</typeparam>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
        where TTransformer : IEncodeTransformer
    {
        return Transformer(typeof(TTransformer), order, metadata);
    }

    /// <summary>
    /// Adds a transformer to the encode pipeline messages published through this publication go
    /// through, on top of any globally registered transformers.
    /// </summary>
    /// <param name="transformerType">The transformer implementation type.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
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

    private ContentType? _defaultContentType;

    /// <summary>
    /// Sets the content type used for messages that do not specify one.
    /// </summary>
    /// <param name="contentType">The default content type. Defaults to <c>text/plain</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultContentType(string contentType)
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
    public InMemoryPublicationConfigurator DefaultDataSchema(Uri dataSchema)
    {
        _defaultDataSchema = dataSchema;
        return this;
    }

    private Uri? _defaultSource;

    /// <summary>
    /// Sets the source (the CloudEvents <c>source</c> attribute) set on messages that do not
    /// specify one.
    /// </summary>
    /// <param name="source">The default source URI.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultSource(Uri source)
    {
        _defaultSource = source;
        return this;
    }

    private string? _defaultSubject;

    /// <summary>
    /// Sets the subject (the CloudEvents <c>subject</c> attribute) set on messages that do not
    /// specify one.
    /// </summary>
    /// <param name="subject">The default subject.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultSubject(string subject)
    {
        _defaultSubject = subject;
        return this;
    }

    private string? _defaultType;

    /// <summary>
    /// Sets the type (the CloudEvents <c>type</c> attribute) set on messages that do not specify
    /// one.
    /// </summary>
    /// <param name="type">The default type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private string? _defaultReplyTo;

    /// <summary>
    /// Sets the address replies to messages published through this publication should be sent to,
    /// when the message does not specify one.
    /// </summary>
    /// <param name="replyTo">The default reply-to address.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultReplyTo(string replyTo)
    {
        _defaultReplyTo = replyTo;
        return this;
    }

    private readonly Dictionary<string, object> _defaultHeaders = [];

    /// <summary>
    /// Adds a header set on every message published through this publication, unless the message
    /// already carries the same header.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator DefaultHeader(string key, object value)
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
    public InMemoryPublicationConfigurator AdditionalCloudEvent(string key, object value)
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
    public InMemoryPublicationConfigurator CloudEventType(CloudEventType cloudEventType)
    {
        _cloudEventType = cloudEventType;
        return this;
    }

    private IPublicationProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the in-memory queue resources this publication needs.
    /// </summary>
    /// <param name="provisioner">The publication provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator Provisioner(IPublicationProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the queue already exists and performs no provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator AssumeExists()
    {
        _provisioner = new AssumeQueueExists();
        return this;
    }

    /// <summary>
    /// Validates the queue exists, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateQueueExists();
        return this;
    }

    /// <summary>
    /// Creates or replaces the queue channel used by this publication.
    /// </summary>
    /// <param name="configure">A delegate that configures how the queue is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationConfigurator CreateOrOverride(Action<CreateOrOverrideConfigurator> configure)
    {
        var cfg = new CreateOrOverrideConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
        return this;
    }

    internal InMemoryPublication ToPublication()
    {
        if (string.IsNullOrEmpty(_routingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a publication. Call RoutingKey to configure it.");
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

        return new InMemoryPublication
        {
            Name = _name ?? Uuid.NewGuid().ToString(),
            RoutingKey = _routingKey!,
            QueueName = _queueName ?? _routingKey!,
            MessageMapperType = _messageMapperType,
            Provisioner = _provisioner,
            Transformers = [.. transformers.OrderBy(x => x.Order)],
            DefaultContentType = _defaultContentType ?? new ContentType("text/plain"),
            DefaultDataSchema = _defaultDataSchema,
            DefaultSource = _defaultSource ?? new Uri("amanhecer", UriKind.RelativeOrAbsolute),
            DefaultSubject = _defaultSubject,
            DefaultType = _defaultType,
            DefaultReplyTo = _defaultReplyTo,
            DefaultHeaders = _defaultHeaders,
            AdditionalCloudEvents = _additionalCloudEvents,
            CloudEventType = _cloudEventType ?? Abstractions.Messaging.CloudEventType.Binary
        };
    }

    /// <summary>
    /// Configures how the in-memory queue channel is created or replaced.
    /// </summary>
    public class CreateOrOverrideConfigurator
    {
        private int _capacity = -1;
        private System.Threading.Channels.BoundedChannelFullMode _fullMode =
            System.Threading.Channels.BoundedChannelFullMode.Wait;

        /// <summary>
        /// Sets the queue capacity. Values less than or equal to <c>0</c> create an unbounded
        /// channel.
        /// </summary>
        /// <param name="capacity">The queue capacity.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateOrOverrideConfigurator Capacity(int capacity)
        {
            _capacity = capacity;
            return this;
        }

        /// <summary>
        /// Sets the behavior applied when a bounded channel is full.
        /// </summary>
        /// <param name="fullMode">The bounded channel full mode.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateOrOverrideConfigurator FullMode(System.Threading.Channels.BoundedChannelFullMode fullMode)
        {
            _fullMode = fullMode;
            return this;
        }

        internal CreateOrOverrideQueue ToProvisioner()
        {
            return new CreateOrOverrideQueue
            {
                Capacity = _capacity,
                FullMode = _fullMode
            };
        }
    }
}
