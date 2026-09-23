using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.ConfluentKafka.Provisioners;
using Amanhecer.Messaging.Transformers;
using Confluent.Kafka;
using Uuid = Amanhecer.Abstractions.Uuid;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures a Kafka publication: the topic messages are published to, the message mapper
/// used to build them, and the default message attributes applied to them.
/// </summary>
public class ConfluentKafkaPublicationConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the publication. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The publication name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator Name(string name)
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
    public ConfluentKafkaPublicationConfigurator RoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _routingKey = routingKey;
        return this;
    }

    private string? _topic;

    /// <summary>
    /// Sets the name of the topic messages are published to. Defaults to the routing key
    /// when not set.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator Topic(string topic)
    {
        if (string.IsNullOrEmpty(topic))
        {
            throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));
        }

        _topic = topic;
        return this;
    }

    private bool _waitForConfirmation;

    /// <summary>
    /// Sets whether publishing waits for the broker's delivery confirmation, so broker
    /// errors surface as publish exceptions and the publish only completes once the record
    /// is acknowledged. Defaults to <see langword="false"/> (fire and forget).
    /// </summary>
    /// <param name="waitForConfirmation">Whether to wait for the delivery confirmation.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator WaitForConfirmation(bool waitForConfirmation = true)
    {
        _waitForConfirmation = waitForConfirmation;
        return this;
    }

    private Action<ProducerConfig>? _configureProducer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ProducerConfig"/> before the producer of
    /// this publication is created. It runs after the gateway-wide callback, so it can
    /// override the gateway configuration for this publication alone. Replacing it drops the
    /// default <see cref="Partitioner.Murmur2Random"/> partitioner, so set the partitioner
    /// again when other clients have to agree on the partition a key lands on.
    /// </summary>
    /// <param name="configureProducer">The producer configuration callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator ConfigureProducer(Action<ProducerConfig> configureProducer)
    {
        _configureProducer = configureProducer ?? throw new ArgumentNullException(nameof(configureProducer));
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
    public ConfluentKafkaPublicationConfigurator MessageMapper(
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
    public ConfluentKafkaPublicationConfigurator MessageMapper<
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
    public ConfluentKafkaPublicationConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
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
    public ConfluentKafkaPublicationConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
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
    /// Sets the content type set on messages that do not specify one.
    /// </summary>
    /// <param name="contentType">The default content type. Defaults to <c>text/plain</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator DefaultContentType(string contentType)
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
    public ConfluentKafkaPublicationConfigurator DefaultDataSchema(Uri dataSchema)
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
    public ConfluentKafkaPublicationConfigurator DefaultSource(Uri source)
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
    public ConfluentKafkaPublicationConfigurator DefaultSubject(string subject)
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
    public ConfluentKafkaPublicationConfigurator DefaultType(string type)
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
    public ConfluentKafkaPublicationConfigurator DefaultReplyTo(string replyTo)
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
    public ConfluentKafkaPublicationConfigurator DefaultHeader(string key, object value)
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
    public ConfluentKafkaPublicationConfigurator AdditionalCloudEvent(string key, object value)
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
    public ConfluentKafkaPublicationConfigurator CloudEventType(CloudEventType cloudEventType)
    {
        _cloudEventType = cloudEventType;
        return this;
    }

    private IPublicationProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources (topic) this publication
    /// needs before messages can be published through it.
    /// </summary>
    /// <param name="provisioner">The publication provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator Provisioner(IPublicationProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the topic already exists on the cluster and performs no provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator AssumeExists()
    {
        _provisioner = new AssumeTopicExists();
        return this;
    }

    /// <summary>
    /// Validates that the topic exists on the cluster, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateTopicExists();
        return this;
    }

    /// <summary>
    /// Creates the topic on the cluster when it does not already exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the topic is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator CreateIfNotExists(Action<CreateTopicConfigurator> configure)
    {
        var cfg = new CreateTopicConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
        return this;
    }

    /// <summary>
    /// Creates the topic on the cluster when it does not already exist, with the broker
    /// defaults for partitions and replication factor.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationConfigurator CreateIfNotExists()
    {
        _provisioner = new CreateTopic();
        return this;
    }

    internal ConfluentKafkaPublication ToPublication()
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

        var publication = new ConfluentKafkaPublication
        {
            RoutingKey = _routingKey!,
            Topic = _topic ?? _routingKey!,
            WaitForConfirmation = _waitForConfirmation,
            MessageMapperType = _messageMapperType,
            Transformers = [.. transformers.OrderBy(x => x.Order)],
            DefaultHeaders = _defaultHeaders,
            AdditionalCloudEvents = _additionalCloudEvents,
            DefaultDataSchema = _defaultDataSchema,
            DefaultSubject = _defaultSubject,
            DefaultType = _defaultType,
            DefaultReplyTo = _defaultReplyTo,
            CloudEventType = _cloudEventType ?? Abstractions.Messaging.CloudEventType.Binary,
            Provisioner = _provisioner,
            Name = _name ?? Uuid.NewGuid().ToString(),
            DefaultContentType = _defaultContentType ?? new ContentType("text/plain"),
            DefaultSource = _defaultSource ?? new Uri("amanhecer", UriKind.RelativeOrAbsolute)
        };

        if (_configureProducer is not null)
        {
            publication.ConfigureProducer = _configureProducer;
        }

        return publication;
    }
}
