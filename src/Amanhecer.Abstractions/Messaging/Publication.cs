using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Base class for declaring a publication: how messages sent through a given routing key
/// are produced, including the default CloudEvents attributes applied to them.
/// </summary>
public abstract class Publication : IPublication
{
    /// <summary>
    /// Gets or sets the name of the publication. Defaults to a randomly generated UUID.
    /// </summary>
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    /// <inheritdoc cref="IPublication.AdditionalCloudEvents"/>
    public Dictionary<string, object> AdditionalCloudEvents { get; set; } = [];

    /// <inheritdoc cref="IPublication.DefaultContentType"/>
    public ContentType DefaultContentType { get; set; } = new("text/plain");

    /// <inheritdoc cref="IPublication.DefaultDataSchema"/>
    public Uri? DefaultDataSchema { get; set; }

    /// <inheritdoc cref="IPublication.DefaultHeaders"/>
    public Dictionary<string, object> DefaultHeaders { get; set; } = [];

    /// <inheritdoc cref="IPublication.RoutingKey"/>
    public required string RoutingKey { get; set; }

    /// <inheritdoc cref="IPublication.DefaultReplyTo"/>
    public string? DefaultReplyTo { get; set; }

    /// <inheritdoc cref="IPublication.DefaultSubject"/>
    public string? DefaultSubject { get; set; }

    /// <inheritdoc cref="IPublication.DefaultSource"/>
    public Uri DefaultSource { get; set; } = new("amanhecer", UriKind.RelativeOrAbsolute);

    /// <inheritdoc cref="IPublication.DefaultSpecVersion"/>
    public string DefaultSpecVersion { get; set; } = "1.0";

    /// <inheritdoc cref="IPublication.DefaultType"/>
    public string? DefaultType { get; set; }

    /// <inheritdoc cref="IPublication.MessageMapperType"/>
    public Type? MessageMapperType { get; set; }

    /// <inheritdoc cref="IPublication.Provisioner"/>
    public IPublicationProvisioner? Provisioner { get; set; }

    /// <inheritdoc cref="IPublication.CloudEventType"/>
    public CloudEventType CloudEventType { get; set; } = CloudEventType.Binary;
}