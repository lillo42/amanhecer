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
    /// Gets the <see cref="IMessageMapper"/> implementation used to map between the
    /// consumed messages and application requests.
    /// </summary>
    Type MessageMapperType { get; }

    /// <summary>
    /// Gets the provisioner that creates the transport resources this subscription
    /// needs, if any.
    /// </summary>
    ISubscriptionProvisoner? Provisioner { get; }
}