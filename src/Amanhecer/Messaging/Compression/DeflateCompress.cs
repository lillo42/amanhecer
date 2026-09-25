using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Compression;
using CompressionMode = Amanhecer.Abstractions.Messaging.Compression.CompressionMode;

namespace Amanhecer.Messaging.Compression;

/// <summary>
/// Compression settings used by <see cref="DeflateCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The Deflate compression level applied when encoding.</param>
public record DeflateMetadata(CompressionLevel CompressionLevel)
{
    /// <summary>
    /// Gets or sets the predicate that decides whether an outgoing message should be compressed.
    /// </summary>
    public Func<Message, bool> ShouldCompress { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the predicate that decides whether an incoming message should be decompressed.
    /// </summary>
    public Func<Message, bool> ShouldDecompress { get; set; } = message => message.ContentEncoding == "deflate";
}

/// <summary>
/// Declares that <see cref="DeflateCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on, using the configured Deflate compression level.
/// </summary>
/// <param name="compressionLevel">The Deflate compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class DeflateAttribute(CompressionLevel compressionLevel, int order)
    : TransformerAttribute<DeflateCompress>(order)
{
    /// <summary>
    /// Gets the Deflate compression level applied when encoding.
    /// </summary>
    public CompressionLevel CompressionLevel => compressionLevel;

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
    public override object Metadata => new DeflateMetadata(compressionLevel)
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
            DecompressionMode.WhenEncodingMatches => message.ContentEncoding == "deflate",
            _ => throw new NotSupportedException()
        }
    };
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with Deflate and
/// decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class DeflateCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<DeflateMetadata>();
        if (metadata.ShouldCompress(message))
        {
            using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
            await using (var deflateStream =
                         new DeflateStream(outputStream, metadata.CompressionLevel, leaveOpen: true))
            {
                await deflateStream.WriteAsync(message.Payload)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
#else
            using (var deflateStream = new DeflateStream(outputStream, metadata.CompressionLevel, leaveOpen: true))
            {
                var payload = message.Payload.ToArray();
                await deflateStream.WriteAsync(payload, 0, payload.Length)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
#endif

            message.ContentEncoding = "deflate";
            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<DeflateMetadata>();
        if (metadata.ShouldDecompress(message))
        {
            using var inputStream = new MemoryStream(message.Payload.ToArray());
            using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
            await using (var deflateStream =
                         new DeflateStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
#else
            using (var deflateStream = new DeflateStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
#endif
            {
                await deflateStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
            }

            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}