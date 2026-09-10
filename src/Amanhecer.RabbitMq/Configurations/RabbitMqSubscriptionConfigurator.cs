using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.RabbitMq.Provisioners;

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

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _messageMapperType;

    /// <summary>
    /// Sets the <see cref="IMessageMapper"/> implementation used to map between consumed
    /// messages and application requests.
    /// </summary>
    /// <param name="mapper">The message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator MessageMapper(
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
    public RabbitMqSubscriptionConfigurator MessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _messageMapperType = typeof(TMapper);
        return this;
    }

    private int _numberOfConsumers = 1;

    /// <summary>
    /// Sets the number of consumers reading from the subscription's queue. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="numberOfConsumers">The number of consumers.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator NumberOfConsumers(int numberOfConsumers)
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
    /// Sets the size of the buffer of messages prefetched by each consumer. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator BufferSize(int bufferSize)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize),
                "Buffer size must be greater than zero.");
        }

        _bufferSize = bufferSize;
        return this;
    }

    private int _prefetchSize;

    /// <summary>
    /// Sets the prefetch size (the QoS window) in bytes for each consumer. Defaults to
    /// <c>0</c>, meaning no limit. Note that RabbitMQ brokers ignore the prefetch size; use
    /// <see cref="BufferSize"/> to limit how many messages each consumer prefetches.
    /// </summary>
    /// <param name="prefetchSize">The prefetch size in bytes.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator PrefetchSize(int prefetchSize)
    {
        if (prefetchSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prefetchSize),
                "Prefetch size cannot be negative.");
        }

        _prefetchSize = prefetchSize;
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

    private string? _deadLetterQueueRoutingKey;

    /// <summary>
    /// Sets the routing key messages are reposted to when the consumer action moves them
    /// to the dead-letter queue.
    /// </summary>
    /// <param name="routingKey">The dead-letter queue routing key.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator DeadLetterQueueRoutingKey(string routingKey)
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
    public RabbitMqSubscriptionConfigurator InvalidMessageRoutingKey(string routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
        {
            throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
        }

        _invalidMessageRoutingKey = routingKey;
        return this;
    }

    private Func<Message, Exception, IConsumerAction>? _onError;

    /// <summary>
    /// Sets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    /// <param name="onError">The error handling function.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator OnError(Func<Message, Exception, IConsumerAction> onError)
    {
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        return this;
    }

    private ISubscriptionProvisioner? _provisioner;

    /// <summary>
    /// Sets the provisioner that creates the transport resources (queue, bindings, ...) this
    /// subscription needs before messages can be consumed from it.
    /// </summary>
    /// <param name="provisioner">The subscription provisioner.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator Provisioner(ISubscriptionProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the queue and its bindings already exist on the broker and performs no
    /// provisioning.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator AssumeExists()
    {
        _provisioner = new AssumeQueueExists();
        return this;
    }

    /// <summary>
    /// Validates that the queue exists on the broker, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateQueueExists();
        return this;
    }

    /// <summary>
    /// Declares the queue on the broker and binds it to an exchange when they do not already
    /// exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the queue is declared and bound.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionConfigurator CreateIfNotExists(Action<CreateIfNotExistsConfigurator> configure)
    {
        var cfg = new CreateIfNotExistsConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
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

        var subscription = new RabbitMqSubscription(_toRoutingKey!, _queueName!)
        {
            Name = _name ?? Uuid.NewGuid().ToString(),
            MessageMapperType = _messageMapperType,
            NumberOfConsumers = _numberOfConsumers,
            BufferSize = _bufferSize,
            PrefetchSize = (uint)_prefetchSize,
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

    /// <summary>
    /// Configures how the queue is declared on the broker and bound to an exchange when they
    /// do not already exist.
    /// </summary>
    public class CreateIfNotExistsConfigurator
    {
        private Exchange? _exchange;

        /// <summary>
        /// Sets the exchange the queue is bound to.
        /// </summary>
        /// <param name="exchange">The exchange instance.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator Exchange(Exchange exchange)
        {
            _exchange = exchange;
            return this;
        }

        /// <summary>
        /// Sets the exchange the queue is bound to.
        /// </summary>
        /// <param name="configure">A delegate that configures the exchange.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator Exchange(Action<RabbitMqExchangeConfigurator> configure)
        {
            var cfg = new RabbitMqExchangeConfigurator();
            configure.Invoke(cfg);

            _exchange = cfg.ToExchange();
            return this;
        }

        private string? _routingKey;

        /// <summary>
        /// Sets the routing key used for the binding between the queue and the exchange.
        /// </summary>
        /// <param name="routingKey">The binding routing key.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="routingKey"/> is null or empty.</exception>
        public CreateIfNotExistsConfigurator RoutingKey(string routingKey)
        {
            if (string.IsNullOrEmpty(routingKey))
            {
                throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));
            }

            _routingKey = routingKey;
            return this;
        }

        private bool _durable;

        /// <summary>
        /// Sets whether the declared queue survives a broker restart.
        /// </summary>
        /// <param name="durable">Whether the queue is durable.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator Durable(bool durable)
        {
            _durable = durable;
            return this;
        }

        private bool _exclusive;

        /// <summary>
        /// Sets whether the declared queue can only be used by the connection that declared it.
        /// </summary>
        /// <param name="exclusive">Whether the queue is exclusive.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator Exclusive(bool exclusive)
        {
            _exclusive = exclusive;
            return this;
        }

        private bool _autoDelete;

        /// <summary>
        /// Sets whether the declared queue is deleted when it is no longer in use.
        /// </summary>
        /// <param name="autoDelete">Whether the queue is auto-deleted.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator AutoDelete(bool autoDelete)
        {
            _autoDelete = autoDelete;
            return this;
        }

        private readonly Dictionary<string, object?> _queueArguments = [];

        /// <summary>
        /// Adds an argument passed to the queue declaration.
        /// </summary>
        /// <param name="key">The argument name.</param>
        /// <param name="value">The argument value.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator QueueArgument(string key, object? value)
        {
            _queueArguments[key] = value;
            return this;
        }

        private readonly Dictionary<string, object?> _bindArguments = [];

        /// <summary>
        /// Adds an argument passed to the queue binding.
        /// </summary>
        /// <param name="key">The argument name.</param>
        /// <param name="value">The argument value.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        public CreateIfNotExistsConfigurator BindArgument(string key, object? value)
        {
            _bindArguments[key] = value;
            return this;
        }

        internal CreateQueue ToProvisioner()
        {
            if (_exchange is null)
            {
                throw new InvalidOperationException(
                    "An exchange is required to declare the queue binding. Call Exchange to configure it.");
            }

            if (string.IsNullOrEmpty(_routingKey))
            {
                throw new InvalidOperationException(
                    "A routing key is required to declare the queue binding. Call RoutingKey to configure it.");
            }

            return new CreateQueue
            {
                Exchange = _exchange,
                RoutingKey = _routingKey!,
                Durable = _durable,
                Exclusive = _exclusive,
                AutoDelete = _autoDelete,
                QueueArguments = _queueArguments,
                BindArguments = _bindArguments
            };
        }
    }
}
