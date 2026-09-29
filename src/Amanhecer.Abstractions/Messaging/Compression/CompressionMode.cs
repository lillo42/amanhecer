namespace Amanhecer.Abstractions.Messaging.Compression;

/// <summary>
/// Defines the built-in conditions used to decide whether an outgoing payload should be compressed.
/// </summary>
public enum CompressionMode
{
    /// <summary>
    /// Compress the payload when its size is greater than or equal to the configured threshold.
    /// </summary>
    WhenPayloadAtLeastThreshold,

    /// <summary>
    /// Always compress the payload.
    /// </summary>
    Always,

    /// <summary>
    /// Never compress the payload.
    /// </summary>
    Never,
}
