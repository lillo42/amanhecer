using System;
using System.Collections.Generic;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Serializer;

/// <summary>
/// A serializable representation of a <see cref="Message"/>, used to persist messages in
/// an inbox or outbox store.
/// </summary>
public class StoreMessage
{
    /// <summary>
    /// Initializes a new empty instance of the <see cref="StoreMessage"/> class.
    /// </summary>
    public StoreMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreMessage"/> class by copying the
    /// state of the given message.
    /// </summary>
    /// <param name="message">The message to copy the state from.</param>
    public StoreMessage(Message message)
    {
        Baggage = message.Baggage?.ToString();
        ContentEncoding = message.ContentEncoding;
        ContentType = message.ContentType.ToString();
        CorrelationId = message.CorrelationId;
        DataRef = message.DataRef;
        DataSchema = message.DataSchema;
        Headers = message.Headers;
        Id = message.Id;
        Payload = message.Payload.ToArray();
        PartitionKey = message.PartitionKey;
        ReplyTo = message.ReplyTo;
        Source = message.Source;
        SpecVersion = message.SpecVersion;
        Subject = message.Subject;
        Time = message.Time;
        TraceParent = message.TraceParent;
        TraceState = message.TraceState?.ToString();
        Type = message.Type;
    }

    /// <summary>
    /// Gets or sets the W3C Baggage propagated with the message.
    /// </summary>
    public string? Baggage { get; set; }

    /// <summary>
    /// Gets or sets the content encoding applied to the payload bytes, if any.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the content type of the payload (the CloudEvents <c>datacontenttype</c>
    /// attribute).
    /// </summary>
    public string ContentType { get; set; } = "text/plain";

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
    public byte[] Payload { get; set; } = [];

    /// <summary>
    /// Gets or sets the address replies to this message should be sent to.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the subject of the message (the CloudEvents <c>subject</c> attribute).
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents spec version the message conforms to.
    /// </summary>
    public string SpecVersion { get; set; } = Message.DefaultSpecVersion;

    /// <summary>
    /// Gets or sets the context in which the message was produced (the CloudEvents
    /// <c>source</c> attribute).
    /// </summary>
    public Uri Source { get; set; } = Message.DefaultSource;

    /// <summary>
    /// Gets or sets the time the message was produced (the CloudEvents <c>time</c>
    /// attribute). Defaults to the current UTC time.
    /// </summary>
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the type of the event the message describes (the CloudEvents
    /// <c>type</c> attribute).
    /// </summary>
    public string Type { get; set; } = Message.DefaultType;

    /// <summary>
    /// Gets or sets the W3C Trace Context trace parent propagated with the message.
    /// </summary>
    public string? TraceParent { get; set; }

    /// <summary>
    /// Gets or sets the W3C Trace Context trace state propagated with the message.
    /// </summary>
    public string? TraceState { get; set; }
}