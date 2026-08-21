using System;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures a RabbitMQ subscription: the queue messages are consumed from, the routing
/// key they are dispatched with, and the message mapper used to map them to application
/// requests.
/// </summary>
public class RabbitMqSubscriptionConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the subscription. Defaults to a randomly generated UUID when not set.
    /// </summary>
    /// <param name="name">The subscription name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator Name(string name)
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
    public RabbitMqSubscriptionConfigurator ToRoutingKey(string routingKey)
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
    /// Sets the name of the RabbitMQ queue messages are consumed from.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator QueueName(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        _queueName = queueName;
        return this;
    }

    private Type? _messageMapperType;

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <param name="mapper">The message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator MessageMapper(Type mapper)
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
    public RabbitMqSubscriptionConfigurator MessageMapper<TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private int _numberOfConsumer;

    /// <summary>
    /// Sets the number of consumers reading from the subscription's queue.
    /// </summary>
    /// <param name="numberOfConsumer">The number of consumers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator NumberOfConsumer(int numberOfConsumer)
    {
        if (numberOfConsumer < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfConsumer),
                "Number of consumers cannot be negative.");
        }

        _numberOfConsumer = numberOfConsumer;
        return this;
    }

    private int _bufferSize;

    /// <summary>
    /// Sets the size of the buffer of messages prefetched by each consumer.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator BufferSize(int bufferSize)
    {
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize),
                "Buffer size cannot be negative.");
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
    public RabbitMqSubscriptionConfigurator DefaultSpecVersion(string specVersion)
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
    public RabbitMqSubscriptionConfigurator DefaultSource(Uri source)
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
    public RabbitMqSubscriptionConfigurator DefaultType(string type)
    {
        _defaultType = type;
        return this;
    }

    private ISubscriptionProvisoner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources (queue, bindings, ...) this
    /// subscription needs before messages can be consumed from it.
    /// </summary>
    /// <param name="provisioner">The subscription provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator Provisioner(ISubscriptionProvisoner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    internal RabbitMqSubscription ToSubscription()
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

        if (_messageMapperType is null)
        {
            throw new InvalidOperationException(
                "A message mapper is required for a subscription. Call MessageMapper<TMapper> to configure it.");
        }

        return new RabbitMqSubscription
        {
            Name = _name ?? Uuid.NewGuid().ToString(),
            ToRoutingKey = _toRoutingKey!,
            QueueName = _queueName!,
            MessageMapperType = _messageMapperType,
            NumberOfConsumer = _numberOfConsumer,
            BufferSize = _bufferSize,
            DefaultSpecVersion = _defaultSpecVersion,
            DefaultSource = _defaultSource ?? new Uri("amanhecer", UriKind.RelativeOrAbsolute),
            DefaultType = _defaultType ?? "default",
            Provisioner = _provisioner
        };
    }
}
