using System;
using System.IO;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Compression;
using Snappier;

namespace Amanhecer.Compression.Snappier;

/// <summary>
/// Compression settings used by <see cref="SnappierCompress"/>.
/// </summary>
public record SnappierMetadata
{
    /// <summary>
    /// Gets or sets the predicate that decides whether an outgoing message should be compressed.
    /// </summary>
    public Func<Message, bool> ShouldCompress { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the predicate that decides whether an incoming message should be decompressed.
    /// </summary>
    public Func<Message, bool> ShouldDecompress { get; set; } = message => message.ContentEncoding == "snappy";
}

/// <summary>
/// Declares that <see cref="SnappierCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on.
/// </summary>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class SnappierAttribute(int order) : TransformerAttribute<SnappierCompress>(order)
{
    /// <summary>
    /// Gets or sets the condition that decides whether outgoing messages should be compressed.
    /// </summary>
    public CompressionMode CompressionMode { get; set; } = CompressionMode.WhenPayloadAtLeastThreshold;

    /// <summary>
    /// Gets or sets the condition that decides whether incoming messages should be decompressed.
    /// </summary>
    public DecompressionMode DecompressionMode { get; set; } = DecompressionMode.WhenEncodingMatches;

    /// <summary>
    /// Gets or sets the minimum payload size, in bytes, required before compression is applied.
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new SnappierMetadata
    {
        ShouldCompress = message => CompressionMode switch
        {
            CompressionMode.Always => true,
            CompressionMode.Never => false,
            CompressionMode.WhenPayloadAtLeastThreshold => message.Payload.Length >= Threshold,
            _ => throw new NotSupportedException()
        },
        ShouldDecompress = message => DecompressionMode switch
        {
            DecompressionMode.Always => true,
            DecompressionMode.Never => false,
            DecompressionMode.WhenEncodingMatches => message.ContentEncoding == "snappy",
            _ => throw new NotSupportedException()
        }
    };
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with Snappy framed
/// streams and decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class SnappierCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<SnappierMetadata>();
        if (metadata.ShouldCompress(message))
        {
            using var outputStream = new MemoryStream();
#if NET8_0_OR_GREATER
            await using (var snappyStream = new SnappyStream(outputStream, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            await snappyStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#else
            using (var snappyStream = new SnappyStream(outputStream, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            var payload = message.Payload.ToArray();
            await snappyStream.WriteAsync(payload, 0, payload.Length)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#endif

            message.ContentEncoding = "snappy";
            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<SnappierMetadata>();
        if (metadata.ShouldDecompress(message))
        {
            using var inputStream = new MemoryStream(message.Payload.ToArray());
            using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
            await using (var snappyStream = new SnappyStream(inputStream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true))
#else
            using (var snappyStream = new SnappyStream(inputStream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true))
#endif
        {
            await snappyStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
