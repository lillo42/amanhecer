namespace Amanhecer.Abstractions.Messaging.Compression;

/// <summary>
/// Defines the built-in conditions used to decide whether an incoming payload should be decompressed.
/// </summary>
public enum DecompressionMode
{
    /// <summary>
    /// Decompress the payload only when the message content encoding matches the transformer encoding.
    /// </summary>
    WhenEncodingMatches,

    /// <summary>
    /// Always attempt to decompress the payload.
    /// </summary>
    Always,

    /// <summary>
    /// Never decompress the payload.
    /// </summary>
    Never
}
