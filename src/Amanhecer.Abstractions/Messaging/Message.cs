using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

public class Message
{
    public Baggage? Baggage { get; set; } = [];
    public ContentType? ContentType { get; set; }
    public string CorrelationId { get; set; } = Uuid.NewGuid().ToString();
    public string? DataRef { get; set; }
    public Uri? DataSchema { get; set; }
    public Dictionary<string, object?> Headers { get; set; } = [];
    public string Id { get; set; } = Uuid.NewGuid().ToString();
    public string? PartitionKey { get; set; }
    public ReadOnlyMemory<byte> Payload { get; set; }
    public string? ReplyTo { get; set; }
    public string? Subject { get; set; }
    public string? SpecVersion { get; set; }
    public Uri? Source { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public string? Type { get; set; }
    public string? TraceParent { get; set; }
    public TraceState? TraceState { get; set; } = [];
}