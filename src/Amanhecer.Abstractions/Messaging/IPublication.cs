using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The declaration of a publication: how messages sent through a given routing key are
/// produced, including the default CloudEvents attributes applied to them.
/// </summary>
public interface IPublication
{
    /// <summary>
    /// Gets or sets additional CloudEvents attributes (extension attributes) set on every
    /// message published through this publication.
    /// </summary>
    Dictionary<string, object> AdditionalCloudEvents { get; set; }

    /// <summary>
    /// Gets or sets the content type set on messages that do not specify one.
    /// </summary>
    ContentType DefaultContentType { get; set; }

    /// <summary>
    /// Gets or sets the schema the message payload conforms to (the CloudEvents
    /// <c>dataschema</c> attribute), set on messages that do not specify one.
    /// </summary>
    Uri? DefaultDataSchema { get; set; }

    /// <summary>
    /// Gets or sets the headers set on every message published through this publication,
    /// unless the message already carries the same header.
    /// </summary>
    Dictionary<string, object> DefaultHeaders { get; set; }

    /// <summary>
    /// Gets or sets the address replies to messages published through this publication
    /// should be sent to, when the message does not specify one.
    /// </summary>
    string? DefaultReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the subject (the CloudEvents <c>subject</c> attribute) set on messages
    /// that do not specify one.
    /// </summary>
    string? DefaultSubject { get; set; }

    /// <summary>
    /// Gets or sets the source (the CloudEvents <c>source</c> attribute) set on messages
    /// that do not specify one.
    /// </summary>
    Uri? DefaultSource { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents spec version set on messages that do not specify one.
    /// </summary>
    string? DefaultSpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the type (the CloudEvents <c>type</c> attribute) set on messages that
    /// do not specify one.
    /// </summary>
    string? DefaultType { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IMessageMapper"/> implementation used to map between
    /// application requests and the messages published through this publication.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type? MessageMapperType { get; set; }

    /// <summary>
    /// Gets or sets the name of the publication.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the routing key messages are published with, used to resolve this
    /// publication and its producer.
    /// </summary>
    string RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the provisioner that creates the transport resources this publication
    /// needs, if any.
    /// </summary>
    IPublicationProvisioner? Provisioner { get; set; }
    
    /// <summary>
    /// Gets or sets the CloudEvents content mode used to encode messages published through
    /// this publication.
    /// </summary>
    CloudEventType CloudEventType { get; set; }

    /// <summary>
    /// Gets the encode transformers applied to messages published through this publication,
    /// on top of any globally registered transformers.
    /// </summary>
    IReadOnlyList<AmanhecerTransformerOptions> Transformers { get; }
}