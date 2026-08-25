using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The declaration of a subscription: how messages consumed from a given routing key are
/// handled, including the default CloudEvents attributes expected on them.
/// </summary>
public interface ISubscription
{
    /// <summary>
    /// Gets the routing key messages are consumed from.
    /// </summary>
    string ToRoutingKey { get; }

    /// <summary>
    /// Gets the CloudEvents spec version expected on consumed messages.
    /// </summary>
    string DefaultSpecVersion { get; }

    /// <summary>
    /// Gets the source (the CloudEvents <c>source</c> attribute) expected on consumed messages.
    /// </summary>
    Uri DefaultSource { get; }

    /// <summary>
    /// Gets the type (the CloudEvents <c>type</c> attribute) expected on consumed messages.
    /// </summary>
    string DefaultType { get; }

    /// <summary>
    /// Gets the name of the subscription.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the number of consumers reading from the subscription.
    /// </summary>
    int NumberOfConsumer { get; }
    
    /// <summary>
    /// Gets the size of the buffer of messages prefetched by each consumer.
    /// </summary>
    int BufferSize { get; }

    /// <summary>
    /// Gets the <see cref="IMessageMapper"/> implementation used to map between the
    /// consumed messages and application requests.
    /// </summary>
    Type? MessageMapperType { get; }

    /// <summary>
    /// Gets the provisioner that creates the transport resources this subscription
    /// needs, if any.
    /// </summary>
    ISubscriptionProvisoner? Provisioner { get; }
    
    /// <summary>
    /// Gets the routing key messages are reposted to when they are moved to the
    /// dead-letter queue, if configured.
    /// </summary>
    string? DeadLetterQueueRoutingKey { get; }

    /// <summary>
    /// Gets the routing key messages are reposted to when they are moved to the
    /// invalid-message destination, if configured.
    /// </summary>
    string? InvalidMessageRoutingKey { get; }

    /// <summary>
    /// Gets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    Func<Message, Exception, IConsumerAction> OnError { get; }
}