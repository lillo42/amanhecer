using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A transport-agnostic message envelope, modelled after CloudEvents: it carries the
/// event metadata (id, subject, source, type, ...), the serialized payload, and the
/// tracing context (trace parent, trace state and baggage).
/// </summary>
public class Message
{
    /// <summary>
    /// Gets or sets the W3C Baggage propagated with the message.
    /// </summary>
    public Baggage? Baggage { get; set; }

    /// <summary>
    /// Gets or sets the content encoding applied to the payload bytes, if any.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// The default content type used by new messages.
    /// </summary>
    public static readonly ContentType DefaultContentType = new("text/plain");

    private ContentType? _contentType = null;
    
    /// <summary>
    /// 
    /// </summary>
    public bool IsContentTypeSet => _contentType != null;
    
    /// <summary>
    /// Gets or sets the content type of the payload (the CloudEvents <c>datacontenttype</c>
    /// attribute).
    /// </summary>
    public ContentType ContentType
    {
        get => _contentType ?? DefaultContentType; 
        set => _contentType = value;
    }

    /// <summary>
    /// Gets or sets the identifier used to correlate this message with others. Defaults
    /// to a randomly generated UUID.
    /// </summary>
    public string CorrelationId { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets a reference to where the payload is stored (the CloudEvents
    /// <c>dataref</c> attribute), used when the payload is kept out of the message.
    /// </summary>
    public string? DataRef { get; set; }

    /// <summary>
    /// Gets or sets the schema the payload conforms to (the CloudEvents <c>dataschema</c>
    /// attribute).
    /// </summary>
    public Uri? DataSchema { get; set; }

    /// <summary>
    /// Gets or sets transport-level headers carried with the message.
    /// </summary>
    public Dictionary<string, object?> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets the message metadata. Metadata is local to the process and is not sent
    /// when the message is published.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the unique identifier of the message (the CloudEvents <c>id</c>
    /// attribute). Defaults to a randomly generated UUID.
    /// </summary>
    public string Id { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the key used by the transport to route messages to a partition, if any.
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload of the message (the CloudEvents <c>data</c>).
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; set; }

    /// <summary>
    /// Gets or sets the address replies to this message should be sent to.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the subject of the message (the CloudEvents <c>subject</c> attribute).
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The default CloudEvents specification version used by new messages.
    /// </summary>
    public const string DefaultSpecVersion = "1.0";

    /// <summary>
    /// Gets or sets the CloudEvents spec version the message conforms to.
    /// </summary>
    public string SpecVersion { get; set; } = DefaultSpecVersion;

    /// <summary>
    /// The default CloudEvents source used by new messages.
    /// </summary>
    public static readonly Uri DefaultSource = new("amanhecer", UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Gets or sets the context in which the message was produced (the CloudEvents
    /// <c>source</c> attribute).
    /// </summary>
    public Uri Source { get; set; } = DefaultSource;

    /// <summary>
    /// Gets or sets the time the message was produced (the CloudEvents <c>time</c>
    /// attribute). Defaults to the current UTC time.
    /// </summary>
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The default CloudEvents type used by new messages.
    /// </summary>
    public const string DefaultType = "Amanhecer";

    /// <summary>
    /// Gets or sets the type of the event the message describes (the CloudEvents
    /// <c>type</c> attribute).
    /// </summary>
    public string Type { get; set; } = DefaultType;

    /// <summary>
    /// Gets or sets the W3C Trace Context trace parent propagated with the message.
    /// </summary>
    public string? TraceParent { get; set; }

    /// <summary>
    /// Gets or sets the W3C Trace Context trace state propagated with the message.
    /// </summary>
    public TraceState? TraceState { get; set; }
}
