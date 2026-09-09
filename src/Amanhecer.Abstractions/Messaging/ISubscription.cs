using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;

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
    /// Gets the content type (the CloudEvents <c>datacontenttype</c> attribute) expected on
    /// consumed messages.
    /// </summary>
    ContentType DefaultContentType { get; }

    /// <summary>
    /// Gets the schema (the CloudEvents <c>dataschema</c> attribute) expected on consumed
    /// message payloads, if any.
    /// </summary>
    Uri? DefaultDataSchema { get; }

    /// <summary>
    /// Gets the address replies should be sent to, expected on consumed messages, if any.
    /// </summary>
    string? DefaultReplyTo { get; }

    /// <summary>
    /// Gets the source (the CloudEvents <c>source</c> attribute) expected on consumed messages.
    /// </summary>
    Uri DefaultSource { get; }

    /// <summary>
    /// Gets the CloudEvents spec version expected on consumed messages.
    /// </summary>
    string DefaultSpecVersion { get; }

    /// <summary>
    /// Gets the subject (the CloudEvents <c>subject</c> attribute) expected on consumed
    /// messages, if any.
    /// </summary>
    string? DefaultSubject { get; }
    
    /// <summary>
    /// Gets the type (the CloudEvents <c>type</c> attribute) expected on consumed messages.
    /// </summary>
    string DefaultType { get; }

    /// <summary>
    /// Gets the name of the subscription.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name of the messaging system messages are consumed from, following the
    /// OpenTelemetry <c>messaging.system</c> semantic convention (for example
    /// <c>rabbitmq</c>, <c>kafka</c> or <c>aws_sqs</c>). Used to tag the metrics and
    /// activities recorded while processing consumed messages.
    /// </summary>
    string MessagingSystem { get; }

    /// <summary>
    /// Gets the number of consumers reading from the subscription.
    /// </summary>
    int NumberOfConsumers { get; }

    /// <summary>
    /// Gets the size of the buffer of messages prefetched by each consumer.
    /// </summary>
    int BufferSize { get; }

    /// <summary>
    /// Gets the delay the consumer waits before polling again when no message was received.
    /// </summary>
    TimeSpan NoMessageDelay { get; }

    /// <summary>
    /// Gets the delay the consumer waits before polling again after receiving messages failed.
    /// </summary>
    TimeSpan FailureDelay { get; }

    /// <summary>
    /// Gets the maximum time the consumer waits for messages on each poll.
    /// </summary>
    TimeSpan ReceiveMessageTimeout { get; }

    /// <summary>
    /// Gets or sets the <see cref="IMessageMapper"/> implementation used to map between the
    /// consumed messages and application requests.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type? MessageMapperType { get; set; }

    /// <summary>
    /// Gets the provisioner that creates the transport resources this subscription
    /// needs, if any.
    /// </summary>
    ISubscriptionProvisioner? Provisioner { get; }

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
    /// Gets a value indicating whether awaits while processing the consumed messages should
    /// continue on the captured synchronization context.
    /// </summary>
    bool ContinueOnCapturedContext { get; }

    /// <summary>
    /// Gets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    Func<Message, Exception, IConsumerAction> OnError { get; }
}