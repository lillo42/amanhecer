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
/// Compression settings used by <see cref="GZipCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The GZip compression level applied when encoding.</param>
public record GZipMetadata(CompressionLevel CompressionLevel)
{
    /// <summary>
    /// Gets or sets the predicate that decides whether an outgoing message should be compressed.
    /// </summary>
    public Func<Message, bool> ShouldCompress { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the predicate that decides whether an incoming message should be decompressed.
    /// </summary>
    public Func<Message, bool> ShouldDecompress { get; set; } = message => message.ContentEncoding == "gzip";
}

/// <summary>
/// Declares that <see cref="GZipCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on, using the configured GZip compression level.
/// </summary>
/// <param name="compressionLevel">The GZip compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class GZipAttribute(CompressionLevel compressionLevel, int order) : TransformerAttribute<GZipCompress>(order)
{
    /// <summary>
    /// Gets the GZip compression level applied when encoding.
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
    public override object Metadata => new GZipMetadata(compressionLevel)
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
            DecompressionMode.WhenEncodingMatches => message.ContentEncoding == "gzip",
            _ => throw new NotSupportedException()
        }
    };
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with GZip and
/// decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class GZipCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<GZipMetadata>();
        if (metadata.ShouldCompress(message))
        {
            using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
            await using (var gzipStream = new GZipStream(outputStream, metadata.CompressionLevel, leaveOpen: true))
            {
                await gzipStream.WriteAsync(message.Payload)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
#else
            using (var gzipStream = new GZipStream(outputStream, metadata.CompressionLevel, leaveOpen: true))
            {
                var payload = message.Payload.ToArray();
                await gzipStream.WriteAsync(payload, 0, payload.Length)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
#endif

            message.ContentEncoding = "gzip";
            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<GZipMetadata>();
        if (metadata.ShouldDecompress(message))
        {
            using var inputStream = new MemoryStream(message.Payload.ToArray());
            using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
            await using (var gzipStream = new GZipStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
#else
            using (var gzipStream = new GZipStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
#endif
            {
                await gzipStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
            }

            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}