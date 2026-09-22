using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Base.Tests;

public class MessageBuilder
{
    private Baggage? _baggage;


    public MessageBuilder SetBaggage(Baggage? baggage)
    {
        _baggage = baggage;
        return this;
    }

    private ContentType _contentType = new("text/plain");

    public MessageBuilder SetContentType(string contentType)
    {
        return SetContentType(new ContentType(contentType));
    }

    public MessageBuilder SetContentType(ContentType contentType)
    {
        _contentType = contentType;
        return this;
    }

    private string _correlationId = Uuid.NewGuid().ToString();

    public MessageBuilder SetCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    private string? _dataRef;

    public MessageBuilder SetDataRef(string? dataRef)
    {
        _dataRef = dataRef;
        return this;
    }

    private Uri? _dataSchema;

    public MessageBuilder SetDataSchema(string? dataSchema)
    {
        _dataSchema = dataSchema == null ? null : new Uri(dataSchema, UriKind.RelativeOrAbsolute);
        return this;
    }

    private Dictionary<string, object?> _headers = new();


    public MessageBuilder SetDataSchema(Uri? dataSchema)
    {
        _dataSchema = dataSchema;
        return this;
    }

    public MessageBuilder SetHeaders(Dictionary<string, object?> headers)
    {
        _headers = headers;
        return this;
    }

    public MessageBuilder AddHeader(string key, object? value)
    {
        _headers.Add(key, value);
        return this;
    }

    public MessageBuilder SetHeader(string key, object? value)
    {
        _headers[key] = value;
        return this;
    }

    private Dictionary<string, object?> _metadata = new();

    public MessageBuilder SetMetadata(Dictionary<string, object?> metadata)
    {
        _metadata = metadata;
        return this;
    }

    public MessageBuilder AddMetadata(string key, object? value)
    {
        _metadata.Add(key, value);
        return this;
    }

    public MessageBuilder SetMetadata(string key, object? value)
    {
        _metadata[key] = value;
        return this;
    }

    private string? _id;

    public MessageBuilder SetId(string id)
    {
        _id = id;
        return this;
    }

    private string? _partitionKey;

    public MessageBuilder SetPartitionKey(string? partitionKey)
    {
        _partitionKey = partitionKey;
        return this;
    }

    private ReadOnlyMemory<byte> _payload = Array.Empty<byte>();


    public MessageBuilder SetPayload(string payload)
    {
        return SetPayload(Encoding.UTF8.GetBytes(payload));
    }

    public MessageBuilder SetPayload(ReadOnlyMemory<byte> payload)
    {
        _payload = payload;
        return this;
    }

    private string? _replyTo;


    public MessageBuilder SetReplyTo(string? replyTo)
    {
        _replyTo = replyTo;
        return this;
    }

    private string? _subject;


    public MessageBuilder SetSubject(string? subject)
    {
        _subject = subject;
        return this;
    }

    private string _specVersion = Message.DefaultSpecVersion;

    public MessageBuilder SetSpecVersion(string specVersion)
    {
        _specVersion = specVersion;
        return this;
    }

    private Uri _source = Message.DefaultSource;

    public MessageBuilder SetSource(string source)
    {
        _source = new Uri(source, UriKind.RelativeOrAbsolute);
        return this;
    }


    public MessageBuilder SetSource(Uri source)
    {
        _source = source;
        return this;
    }

    private DateTimeOffset _time = DateTimeOffset.UtcNow;

    public MessageBuilder SetTime(DateTimeOffset time)
    {
        _time = time;
        return this;
    }

    private string _type = Message.DefaultType;

    public MessageBuilder SetType(string type)
    {
        _type = type;
        return this;
    }

    private string? _traceParent;

    public MessageBuilder SetTraceParent(string? traceParent)
    {
        _traceParent = traceParent;
        return this;
    }

    private TraceState? _traceState;

    public MessageBuilder SetTraceState(string? traceState)
    {
        _traceState = traceState == null ? null : TraceState.FromString(traceState);
        return this;
    }

    public MessageBuilder SetTraceState(TraceState? traceState)
    {
        _traceState = traceState;
        return this;
    }

    public Message Build()
    {
        return new Message
        {
            Baggage = _baggage,
            ContentType = _contentType,
            CorrelationId = _correlationId,
            DataRef = _dataRef,
            DataSchema = _dataSchema,
            Headers = _headers,
            Id = _id ?? Uuid.NewGuid().ToString(),
            Metadata = _metadata,
            PartitionKey = _partitionKey,
            Payload = _payload,
            ReplyTo = _replyTo,
            Subject = _subject,
            SpecVersion = _specVersion,
            Source = _source,
            Time = _time,
            Type = _type,
            TraceParent = _traceParent,
            TraceState = _traceState,
        };
    }
}