using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Amanhencer.Abstractions;

public class ExternalMessage
{
    public const string DefaultSpecVersion = "1.0";
    public const string DefaultSource = "amanhancer";

    public required Memory<byte> Body { get; set; }
    public ContentType ContentType { get; set; } = new("application/text");
    public Baggage? Baggage { get; set; }
    public Uri? DataSchema { get; set; }
    public string? DataRef { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
    public required string Id { get; set; } = Uuid.NewGuid().ToString();
    public string? PartitionKey { get; set; }
    public string? Subject { get; set; }
    public string SpecVersion { get; set; } = DefaultSpecVersion;
    public Uri Source { get; set; } = new(DefaultSource, UriKind.Relative);
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;
    public string? TraceParent { get; set; }
    public TraceState? TraceState { get; set; }
    public string? Type { get; set; }
}
