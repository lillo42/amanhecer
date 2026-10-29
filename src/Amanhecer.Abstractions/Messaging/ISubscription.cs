using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Amanhecer.Abstractions.Messaging.Inboxes;

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
    string ToRoutingKey { get; set; }

    /// <summary>
    /// Gets the content type (the CloudEvents <c>datacontenttype</c> attribute) expected on
    /// consumed messages.
    /// </summary>
    ContentType DefaultContentType { get; set;  }

    /// <summary>
    /// Gets the schema (the CloudEvents <c>dataschema</c> attribute) expected on consumed
    /// message payloads, if any.
    /// </summary>
    Uri? DefaultDataSchema { get; set; }

    /// <summary>
    /// Gets the address replies should be sent to, expected on consumed messages, if any.
    /// </summary>
    string? DefaultReplyTo { get; set;  }

    /// <summary>
    /// Gets the source (the CloudEvents <c>source</c> attribute) expected on consumed messages.
    /// </summary>
    Uri DefaultSource { get; set;  }

    /// <summary>
    /// Gets the CloudEvents spec version expected on consumed messages.
    /// </summary>
    string DefaultSpecVersion { get; set; }

    /// <summary>
    /// Gets the subject (the CloudEvents <c>subject</c> attribute) expected on consumed
    /// messages, if any.
    /// </summary>
    string? DefaultSubject { get; set; }

    /// <summary>
    /// Gets the type (the CloudEvents <c>type</c> attribute) expected on consumed messages.
    /// </summary>
    string DefaultType { get; set; }

    /// <summary>
    /// Gets the CloudEvents content mode expected on consumed messages: binary (attributes
    /// in transport headers) or structured JSON envelope.
    /// </summary>
    CloudEventType CloudEventType { get; set; }

    /// <summary>
    /// Gets the name of the subscription.
    /// </summary>
    string Name { get; set; }

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
    int NumberOfConsumers { get; set; }

    /// <summary>
    /// Gets the size of the buffer used to hold messages prefetched from the transport.
    /// </summary>
    int BufferSize { get; set; }

    /// <summary>
    /// Gets the delay the consumer waits before polling again when no message was received.
    /// </summary>
    TimeSpan NoMessageDelay { get; set; }

    /// <summary>
    /// Gets the delay the consumer waits before polling again when receiving or processing
    /// messages fails.
    /// </summary>
    TimeSpan FailureDelay { get; set; }

    /// <summary>
    /// Gets the maximum time the consumer waits for messages on each poll.
    /// </summary>
    TimeSpan ReceiveMessageTimeout { get; set; }

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
    ISubscriptionProvisioner? Provisioner { get; set; }

    /// <summary>
    /// Gets the routing key messages are reposted to when they are moved to the
    /// dead-letter queue, if configured.
    /// </summary>
    string? DeadLetterQueueRoutingKey { get; set; }

    /// <summary>
    /// Gets the routing key messages are reposted to when they are moved to the
    /// invalid-message destination, if configured.
    /// </summary>
    string? InvalidMessageRoutingKey { get; set; }

    /// <summary>
    /// Gets a value indicating whether awaits while processing the consumed messages should
    /// continue on the captured synchronization context.
    /// </summary>
    bool ContinueOnCapturedContext { get; set; }

    /// <summary>
    /// Gets the function that maps an exception thrown while handling a consumed message
    /// to the <see cref="IConsumerAction"/> used to settle it.
    /// </summary>
    Func<Message, Exception, IConsumerAction> OnError { get; set; }

    /// <summary>
    /// Gets the decode transformers applied to messages consumed through this subscription,
    /// on top of any globally registered transformers.
    /// </summary>
    IReadOnlyList<AmanhecerTransformerOptions> Transformers { get; set; }

    /// <summary>
    /// Gets the maximum time allowed to process a batch of consumed messages.
    /// </summary>
    TimeSpan BatchProcessingTimeout { get; set; }

    /// <summary>
    /// Gets the maximum time allowed to process a single consumed message.
    /// </summary>
    TimeSpan MessageProcessingTimeout { get; set; }
    
    /// <summary>
    /// Gets the strategy used to process batches of consumed messages.
    /// </summary>
    IBatchProcessingStrategy? BatchProcessingStrategy { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    IInbox? Inbox { get; set; }
}