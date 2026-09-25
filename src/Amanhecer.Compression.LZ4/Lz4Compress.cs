using System;
using System.IO;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace Amanhecer.Compression.LZ4;

/// <summary>
/// Compression settings used by <see cref="Lz4Compress"/>.
/// </summary>
/// <param name="CompressionLevel">The LZ4 compression level applied when encoding.</param>
public record Lz4Metadata(LZ4Level CompressionLevel)
{
    /// <summary>
    /// Gets or sets the predicate that decides whether an outgoing message should be compressed.
    /// </summary>
    public Func<Message, bool> ShouldCompress { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the predicate that decides whether an incoming message should be decompressed.
    /// </summary>
    public Func<Message, bool> ShouldDecompress { get; set; } = message => message.ContentEncoding == "lz4";
}

/// <summary>
/// Declares that <see cref="Lz4Compress"/> applies to the messages mapped by the message mapper
/// type the attribute is placed on, using the configured LZ4 compression level.
/// </summary>
/// <param name="compressionLevel">The LZ4 compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class Lz4Attribute(LZ4Level compressionLevel, int order) : TransformerAttribute<Lz4Compress>(order)
{
    /// <summary>
    /// Gets the LZ4 compression level applied when encoding.
    /// </summary>
    public LZ4Level CompressionLevel => compressionLevel;

    /// <summary>
    /// Gets or sets the condition that decides whether outgoing messages should be compressed.
    /// </summary>
    public CompressionMode CompressionMode { get; set; } =
        CompressionMode.WhenPayloadAtLeastThreshold;

    /// <summary>
    /// Gets or sets the condition that decides whether incoming messages should be decompressed.
    /// </summary>
    public DecompressionMode DecompressionMode { get; set; } =
        DecompressionMode.WhenEncodingMatches;

    /// <summary>
    /// Gets or sets the minimum payload size, in bytes, required before compression is applied.
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new Lz4Metadata(CompressionLevel)
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
            DecompressionMode.WhenEncodingMatches => message.ContentEncoding == "lz4",
            _ => throw new NotSupportedException()
        }
    };
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with framed LZ4 and
/// decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class Lz4Compress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<Lz4Metadata>();
        if (metadata.ShouldCompress(message))
        {
            var payload = message.Payload.ToArray();

            using var outputStream = new MemoryStream();
            using (var lz4Stream = LZ4Stream.Encode(outputStream,
                       new LZ4EncoderSettings { CompressionLevel = metadata.CompressionLevel },
                       leaveOpen: true))
            {
                await lz4Stream.WriteAsync(payload, 0, payload.Length)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }

            message.ContentEncoding = "lz4";
            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<Lz4Metadata>();
        if (metadata.ShouldDecompress(message))
        {
            using var inputStream = new MemoryStream(message.Payload.ToArray());
            using var outputStream = new MemoryStream();
            using (var lz4Stream = LZ4Stream.Decode(inputStream, leaveOpen: true))
            {
                await lz4Stream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
            }

            message.Payload = outputStream.ToArray();
        }

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
