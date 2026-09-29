using System;
using System.Diagnostics.CodeAnalysis;
using Uuid = Amanhecer.Abstractions.Uuid;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.ConfluentKafka.Provisioners;
using Amanhecer.Messaging.Compression;
using Amanhecer.Messaging.Transformers;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures a Kafka subscription: the topic and consumer group messages are consumed
/// from, the routing key they are dispatched with, and the message mapper used to map them
/// to application requests.
/// </summary>
public class ConfluentKafkaSubscriptionConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the subscription. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The subscription name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator Name(string name)
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
    public ConfluentKafkaSubscriptionConfigurator ToRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _toRoutingKey = routingKey;
        return this;
    }

    private string? _topic;

    /// <summary>
    /// Sets the name of the topic messages are consumed from. Defaults to the routing key
    /// when not set.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator Topic(string topic)
    {
        if (string.IsNullOrEmpty(topic))
        {
            throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));
        }

        _topic = topic;
        return this;
    }

    private string? _groupId;

    /// <summary>
    /// Sets the consumer group the consumers join. Consumers in the same group share the
    /// topic partitions between them. Defaults to the routing key when not set.
    /// </summary>
    /// <param name="groupId">The consumer group id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator GroupId(string groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            throw new ArgumentException("Group id cannot be null or empty.", nameof(groupId));
        }

        _groupId = groupId;
        return this;
    }

    private Confluent.Kafka.AutoOffsetReset _autoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;

    /// <summary>
    /// Sets where consumption starts when there is no committed offset for the consumer
    /// group. Defaults to <see cref="Confluent.Kafka.AutoOffsetReset.Earliest"/>.
    /// </summary>
    /// <param name="autoOffsetReset">The offset reset strategy.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator AutoOffsetReset(AutoOffsetReset autoOffsetReset)
    {
        _autoOffsetReset = autoOffsetReset;
        return this;
    }

    private long _commitBatchSize = 10;

    /// <summary>
    /// Sets how many acknowledged offsets are stored before they are committed to the
    /// broker. Defaults to <c>10</c>.
    /// </summary>
    /// <param name="commitBatchSize">The commit batch size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator CommitBatchSize(long commitBatchSize)
    {
        if (commitBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commitBatchSize),
                "Commit batch size must be greater than zero.");
        }

        _commitBatchSize = commitBatchSize;
        return this;
    }

    private TimeSpan _sweepUncommittedOffsetsInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sets the interval after which stored-but-uncommitted offsets are committed by the
    /// sweeper, so partially complete batches on low-traffic topics do not linger
    /// uncommitted. Defaults to 30 seconds.
    /// </summary>
    /// <param name="interval">The sweep interval.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator SweepUncommittedOffsetsInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval),
                "The sweep interval must be greater than zero.");
        }

        _sweepUncommittedOffsetsInterval = interval;
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
    public ConfluentKafkaSubscriptionConfigurator MessageMapper(
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
    public ConfluentKafkaSubscriptionConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private int _numberOfConsumers = 1;

    /// <summary>
    /// Sets the number of consumers reading from the subscription's topic. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="numberOfConsumers">The number of consumers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator NumberOfConsumers(int numberOfConsumers)
    {
        if (numberOfConsumers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfConsumers),
                "Number of consumers must be greater than zero.");
        }

        _numberOfConsumers = numberOfConsumers;
        return this;
    }

    private Action<ConsumerConfig>? _configureConsumer;

    /// <summary>
    /// Sets a callback invoked with the <see cref="ConsumerConfig"/> before the consumer of
    /// this subscription is created. It runs after the gateway-wide callback, so it can
    /// override the gateway configuration for this subscription alone.
    /// </summary>
    /// <param name="configureConsumer">The consumer configuration callback.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator ConfigureConsumer(Action<ConsumerConfig> configureConsumer)
    {
        _configureConsumer = configureConsumer ?? throw new ArgumentNullException(nameof(configureConsumer));
        return this;
    }

    private int _bufferSize = 1;

    /// <summary>
    /// Sets how many messages a single poll returns at most: the consumer takes the records
    /// the client has already fetched, up to this many, so they are processed as one batch.
    /// Defaults to <c>1</c>.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator BufferSize(int bufferSize)
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
    public ConfluentKafkaSubscriptionConfigurator DefaultSpecVersion(string specVersion)
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
    public ConfluentKafkaSubscriptionConfigurator DefaultSource(Uri source)
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
    public ConfluentKafkaSubscriptionConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private string? _deadLetterQueueRoutingKey;

    /// <summary>
    /// Sets the routing key messages are reposted to when the consumer action moves them
    /// to the dead-letter queue.
    /// </summary>
    /// <param name="routingKey">The dead-letter queue routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator DeadLetterQueueRoutingKey(string routingKey)
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
    public ConfluentKafkaSubscriptionConfigurator InvalidMessageRoutingKey(string routingKey)
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
    public ConfluentKafkaSubscriptionConfigurator CloudEvent(CloudEventType cloudEvent)
    {
        _cloudEventType = cloudEvent;
        return this;
    }

    /// <summary>
    /// Configures the subscription to consume structured CloudEvents JSON envelopes.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator JsonCloudEvent()
    {
        return CloudEvent(CloudEventType.Json);
    }

    /// <summary>
    /// Configures the subscription to consume binary CloudEvents (attributes in headers).
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator BinaryCloudEvent()
    {
        return CloudEvent(CloudEventType.Binary);
    }

    private Func<Message, Exception, IConsumerAction>? _onError;

    /// <summary>
    /// Sets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    /// <param name="onError">The error handling function.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator OnError(Func<Message, Exception, IConsumerAction> onError)
    {
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        return this;
    }

    private ISubscriptionProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources (topic) this subscription
    /// needs before messages can be consumed from it.
    /// </summary>
    /// <param name="provisioner">The subscription provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator Provisioner(ISubscriptionProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the topic already exists on the cluster and performs no provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator AssumeExists()
    {
        _provisioner = new AssumeTopicExists();
        return this;
    }

    /// <summary>
    /// Validates that the topic exists on the cluster, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateTopicExists();
        return this;
    }

    /// <summary>
    /// Creates the topic on the cluster when it does not already exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the topic is created.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionConfigurator CreateIfNotExists(Action<CreateTopicConfigurator> configure)
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
    public ConfluentKafkaSubscriptionConfigurator CreateIfNotExists()
    {
        _provisioner = new CreateTopic();
        return this;
    }

    internal ConfluentKafkaSubscription ToSubscription()
    {
        if (string.IsNullOrEmpty(_toRoutingKey))
        {
            throw new InvalidOperationException(
                "A routing key is required for a subscription. Call ToRoutingKey to configure it.");
        }

        var subscription = new ConfluentKafkaSubscription(_toRoutingKey!,
            _topic ?? _toRoutingKey!,
            _groupId ?? _toRoutingKey!)
        {
            Name = _name ?? Uuid.NewGuid().ToString(),
            MessageMapperType = _messageMapperType,
            CloudEventType = _cloudEventType,
            // The envelope unwrap is content-type-sniffing and runs first, so it is inert
            // for binary-mode messages.
            Transformers = [new AmanhecerTransformerOptions(typeof(StructuredCloudEventTransformer), int.MinValue, null)],
            AutoOffsetReset = _autoOffsetReset,
            CommitBatchSize = _commitBatchSize,
            SweepUncommittedOffsetsInterval = _sweepUncommittedOffsetsInterval,
            Configure = _configureConsumer,
            NumberOfConsumers = _numberOfConsumers,
            BufferSize = _bufferSize,
            DefaultSpecVersion = _defaultSpecVersion,
            DefaultSource = _defaultSource ?? new Uri("amanhecer", UriKind.RelativeOrAbsolute),
            DefaultType = _defaultType ?? "default",
            Provisioner = _provisioner,
            DeadLetterQueueRoutingKey = _deadLetterQueueRoutingKey,
            InvalidMessageRoutingKey = _invalidMessageRoutingKey
        };

        if (_onError is not null)
        {
            subscription.OnError = _onError;
        }

        return subscription;
    }
}
