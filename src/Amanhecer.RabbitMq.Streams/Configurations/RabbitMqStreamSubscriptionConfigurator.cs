using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures a RabbitMQ Streams subscription: the stream messages are consumed from, the
/// routing key they are dispatched with, and the message mapper used to map them.
/// </summary>
public class RabbitMqStreamSubscriptionConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the subscription. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The subscription name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Name(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Subscription name cannot be null or empty.", nameof(name));
        }

        _name = name;
        return this;
    }

    private string? _toRoutingKey;

    /// <summary>
    /// Sets the routing key consumed messages are dispatched to.
    /// </summary>
    /// <param name="routingKey">The routing key to dispatch consumed messages to.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator ToRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _toRoutingKey = routingKey;
        return this;
    }

    private string? _stream;

    /// <summary>
    /// Sets the name of the RabbitMQ stream messages are consumed from.
    /// </summary>
    /// <param name="stream">The stream name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Stream(string stream)
    {
        if (string.IsNullOrEmpty(stream))
        {
            throw new ArgumentException("Stream name cannot be null or empty.", nameof(stream));
        }

        _stream = stream;
        return this;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _messageMapperType;

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <param name="mapper">The message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator MessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type mapper)
    {
        _messageMapperType = mapper;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <typeparam name="TMapper">The message mapper implementation type.</typeparam>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private int _numberOfConsumers = 1;

    /// <summary>
    /// Sets the number of consumers reading from the subscription's stream. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="numberOfConsumers">The number of consumers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator NumberOfConsumers(int numberOfConsumers)
    {
        if (numberOfConsumers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfConsumers),
                "Number of consumers must be greater than zero.");
        }

        _numberOfConsumers = numberOfConsumers;
        return this;
    }

    private int _bufferSize = 1;

    /// <summary>
    /// Sets the size of the buffer used as the initial credits (prefetch window) for the stream
    /// consumer. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator BufferSize(int bufferSize)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize),
                "Buffer size must be greater than zero.");
        }

        _bufferSize = bufferSize;
        return this;
    }

    private string _defaultSpecVersion = "1.0";

    /// <summary>
    /// Sets the CloudEvents spec version expected on consumed messages.
    /// </summary>
    /// <param name="specVersion">The spec version. Defaults to <c>1.0</c>.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator DefaultSpecVersion(string specVersion)
    {
        if (string.IsNullOrEmpty(specVersion))
        {
            throw new ArgumentException("Spec version cannot be null or empty.", nameof(specVersion));
        }

        _defaultSpecVersion = specVersion;
        return this;
    }

    private Uri? _defaultSource;

    /// <summary>
    /// Sets the source (the CloudEvents <c>source</c> attribute) expected on consumed messages.
    /// </summary>
    /// <param name="source">The default source URI.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator DefaultSource(Uri source)
    {
        _defaultSource = source;
        return this;
    }

    private string? _defaultType;

    /// <summary>
    /// Sets the type (the CloudEvents <c>type</c> attribute) expected on consumed messages.
    /// </summary>
    /// <param name="type">The default type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private string? _deadLetterQueueRoutingKey;

    /// <summary>
    /// Sets the routing key messages are reposted to when the consumer action moves them
    /// to the dead-letter destination.
    /// </summary>
    /// <param name="routingKey">The dead-letter routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator DeadLetterQueueRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _deadLetterQueueRoutingKey = routingKey;
        return this;
    }

    private string? _invalidMessageRoutingKey;

    /// <summary>
    /// Sets the routing key messages are reposted to when the consumer action moves them
    /// to the invalid-message destination.
    /// </summary>
    /// <param name="routingKey">The invalid-message routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator InvalidMessageRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _invalidMessageRoutingKey = routingKey;
        return this;
    }

    private CloudEventType _cloudEventType = CloudEventType.Binary;

    /// <summary>
    /// Sets how consumed messages are interpreted as CloudEvents.
    /// </summary>
    /// <param name="cloudEvent">The CloudEvents encoding mode expected on consumed messages.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator CloudEvent(CloudEventType cloudEvent)
    {
        _cloudEventType = cloudEvent;
        return this;
    }

    /// <summary>
    /// Configures the subscription to consume structured CloudEvents JSON envelopes.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator JsonCloudEvent()
    {
        return CloudEvent(CloudEventType.Json);
    }

    /// <summary>
    /// Configures the subscription to consume binary CloudEvents (attributes in application properties).
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator BinaryCloudEvent()
    {
        return CloudEvent(CloudEventType.Binary);
    }

    private readonly List<AmanhecerTransformerOptions> _transformers = [];

    /// <summary>
    /// Adds a transformer to the decode pipeline messages consumed through this subscription go
    /// through, on top of any globally registered transformers.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer implementation type.</typeparam>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
        where TTransformer : IDecodeTransformer
    {
        return Transformer(typeof(TTransformer), order, metadata);
    }

    /// <summary>
    /// Adds a transformer to the decode pipeline messages consumed through this subscription go
    /// through, on top of any globally registered transformers.
    /// </summary>
    /// <param name="transformerType">The transformer implementation type.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
    {
        if (!typeof(IDecodeTransformer).IsAssignableFrom(transformerType))
        {
            throw new ArgumentException(
                $"The type '{transformerType.FullName}' does not implement IDecodeTransformer.",
                nameof(transformerType));
        }

        _transformers.Add(new AmanhecerTransformerOptions(transformerType, order, metadata));
        return this;
    }

    /// <summary>
    /// Adds an inline transformer to the decode pipeline messages consumed through this
    /// subscription go through, on top of any globally registered transformers. The delegate is
    /// wrapped in an <see cref="AnonymousDecodeTransformer"/>.
    /// </summary>
    /// <param name="func">The delegate executed as the transformer body.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Transformer(
        Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func,
        int order = 0)
    {
        _transformers.Add(new AmanhecerTransformerOptions(typeof(AnonymousDecodeTransformer), order, func));
        return this;
    }

    private Func<Message, Exception, IConsumerAction>? _onError;

    /// <summary>
    /// Sets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    /// <param name="onError">The error handling function.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator OnError(Func<Message, Exception, IConsumerAction> onError)
    {
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        return this;
    }

    private Action<ConsumerConfig>? _configure;

    /// <summary>
    /// Registers a callback to further configure the <see cref="ConsumerConfig"/> before the
    /// consumer is created.
    /// </summary>
    /// <param name="configure">A delegate that receives the <see cref="ConsumerConfig"/> being configured.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Configure(Action<ConsumerConfig> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    private ISubscriptionProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources this subscription needs before
    /// messages can be consumed from it.
    /// </summary>
    /// <param name="provisioner">The subscription provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator Provisioner(ISubscriptionProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the stream already exists on the broker and performs no provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator AssumeExists()
    {
        _provisioner = new AssumeStreamExists();
        return this;
    }

    /// <summary>
    /// Validates that the stream exists on the broker, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateStreamExists();
        return this;
    }

    /// <summary>
    /// Creates the stream on the broker when it does not already exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the stream is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionConfigurator CreateIfNotExists(Action<CreateStreamConfigurator> configure)
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
    public RabbitMqStreamSubscriptionConfigurator CreateIfNotExists()
    {
        _provisioner = new CreateStream();
        return this;
    }

    internal RabbitMqStreamSubscription ToSubscription()
    {
        if (string.IsNullOrEmpty(_toRoutingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a subscription. Call ToRoutingKey to configure it.");
        }

        if (string.IsNullOrEmpty(_stream))
        {
            throw new InvalidOperationException(
                "A stream name is required for a subscription. Call Stream to configure it.");
        }

        var subscription = new RabbitMqStreamSubscription(_toRoutingKey!, _stream!)
        {
            Name = _name ?? Uuid.NewGuid().ToString(),
            MessageMapperType = _messageMapperType,
            CloudEventType = _cloudEventType,
            Transformers = [.. _transformers],
            NumberOfConsumers = _numberOfConsumers,
            BufferSize = _bufferSize,
            DefaultSpecVersion = _defaultSpecVersion,
            DefaultSource = _defaultSource ?? new Uri("amanhecer", UriKind.RelativeOrAbsolute),
            DefaultType = _defaultType ?? "default",
            Provisioner = _provisioner,
            DeadLetterQueueRoutingKey = _deadLetterQueueRoutingKey,
            InvalidMessageRoutingKey = _invalidMessageRoutingKey,
            Configuration = _configure
        };

        if (_onError is not null)
        {
            subscription.OnError = _onError;
        }

        return subscription;
    }
}
