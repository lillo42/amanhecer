using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Provisioners;
using Amanhecer.Messaging.Transformers;

namespace Amanhecer.InMemory.Configurations;

/// <summary>
/// Configures an in-memory subscription: the queue consumed from, the routing key consumed
/// messages are dispatched to, and the message mapper used to map them to application requests.
/// </summary>
public class InMemorySubscriptionConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the subscription. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The subscription name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator Name(string name)
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
    public InMemorySubscriptionConfigurator ToRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _toRoutingKey = routingKey;
        return this;
    }

    private string? _queueName;

    /// <summary>
    /// Sets the name of the in-memory queue messages are consumed from.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator QueueName(string queueName)
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
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <param name="mapper">The message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator MessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type mapper)
    {
        if (!typeof(IMessageMapper).IsAssignableFrom(mapper))
        {
            throw new ArgumentException(
                $"The type '{mapper.FullName}' does not implement IMessageMapper.",
                nameof(mapper));
        }

        _messageMapperType = mapper;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <typeparam name="TMapper">The message mapper implementation type.</typeparam>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private int _numberOfConsumers = 1;

    /// <summary>
    /// Sets the number of consumers reading from the subscription queue. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="numberOfConsumers">The number of consumers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator NumberOfConsumers(int numberOfConsumers)
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
    /// Sets the number of messages pulled from the queue per poll. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator BufferSize(int bufferSize)
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
    public InMemorySubscriptionConfigurator DefaultSpecVersion(string specVersion)
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
    public InMemorySubscriptionConfigurator DefaultSource(Uri source)
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
    public InMemorySubscriptionConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private string? _deadLetterQueueRoutingKey;

    /// <summary>
    /// Sets the routing key messages are reposted to when the consumer action moves them to the
    /// dead-letter destination.
    /// </summary>
    /// <param name="routingKey">The dead-letter queue routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator DeadLetterQueueRoutingKey(string routingKey)
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
    /// Sets the routing key messages are reposted to when the consumer action moves them to the
    /// invalid-message destination.
    /// </summary>
    /// <param name="routingKey">The invalid-message routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator InvalidMessageRoutingKey(string routingKey)
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
    public InMemorySubscriptionConfigurator CloudEvent(CloudEventType cloudEvent)
    {
        _cloudEventType = cloudEvent;
        return this;
    }

    /// <summary>
    /// Configures the subscription to consume structured CloudEvents JSON envelopes.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator JsonCloudEvent()
    {
        return CloudEvent(CloudEventType.Json);
    }

    /// <summary>
    /// Configures the subscription to consume binary CloudEvents (attributes in headers).
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator BinaryCloudEvent()
    {
        return CloudEvent(CloudEventType.Binary);
    }

    private readonly List<AmanhecerTransformerOptions> _transformers =
    [
        new AmanhecerTransformerOptions(typeof(StructuredCloudEventTransformer), int.MinValue, null)
    ];

    /// <summary>
    /// Adds a transformer to the decode pipeline messages consumed through this subscription go
    /// through, on top of any globally registered transformers.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer implementation type.</typeparam>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator Transformer<TTransformer>(int order = 0, object? metadata = null)
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
    public InMemorySubscriptionConfigurator Transformer(Type transformerType, int order = 0, object? metadata = null)
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

    private Func<Message, Exception, IConsumerAction>? _onError;

    /// <summary>
    /// Sets the function that maps an exception thrown while handling a consumed message to the
    /// <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    /// <param name="onError">The error handling function.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator OnError(Func<Message, Exception, IConsumerAction> onError)
    {
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        return this;
    }

    private ISubscriptionProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the in-memory queue resources this subscription needs.
    /// Defaults to <see cref="CreateOrOverrideQueue"/> when not set, because an in-memory queue
    /// can never pre-exist.
    /// </summary>
    /// <param name="provisioner">The subscription provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator Provisioner(ISubscriptionProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the queue already exists and performs no provisioning. The default is
    /// <see cref="CreateOrOverrideQueue"/>; use this only when another publication or
    /// subscription on the same gateway creates the queue.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator AssumeExists()
    {
        _provisioner = new AssumeQueueExists();
        return this;
    }

    /// <summary>
    /// Validates the queue exists, throwing when it does not. The default is
    /// <see cref="CreateOrOverrideQueue"/>; use this only when another publication or
    /// subscription on the same gateway creates the queue.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateQueueExists();
        return this;
    }

    /// <summary>
    /// Creates or replaces the queue channel used by this subscription. This is the default
    /// when no provisioner is configured.
    /// </summary>
    /// <param name="configure">A delegate that configures how the queue is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionConfigurator CreateOrOverride(Action<CreateOrOverrideConfigurator> configure)
    {
        var cfg = new CreateOrOverrideConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
        return this;
    }

    internal InMemorySubscription ToSubscription()
    {
        if (string.IsNullOrEmpty(_toRoutingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a subscription. Call ToRoutingKey to configure it.");
        }

        if (string.IsNullOrEmpty(_queueName))
        {
            throw new InvalidOperationException(
                "A queue name is required for a subscription. Call QueueName to configure it.");
        }

        var subscription = new InMemorySubscription(_toRoutingKey!, _queueName!)
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
            Provisioner = _provisioner ?? new CreateOrOverrideQueue(),
            DeadLetterQueueRoutingKey = _deadLetterQueueRoutingKey,
            InvalidMessageRoutingKey = _invalidMessageRoutingKey
        };

        if (_onError is not null)
        {
            subscription.OnError = _onError;
        }

        return subscription;
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
