using System;
using System.IO;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace Amanhecer.Compression.LZ4;

/// <summary>
/// Compression settings used by <see cref="Lz4Compress"/>.
/// </summary>
/// <param name="CompressionLevel">The LZ4 compression level applied when encoding.</param>
public record Lz4Metadata(LZ4Level CompressionLevel);

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
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new Lz4Metadata(compressionLevel);
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
        var payload = message.Payload.ToArray();

        using var outputStream = new MemoryStream();
        using (var lz4Stream = LZ4Stream.Encode(outputStream,
                   new LZ4EncoderSettings { CompressionLevel = metadata.CompressionLevel },
                   leaveOpen: true))
        {
            await lz4Stream.WriteAsync(payload, 0, payload.Length)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        using var inputStream = new MemoryStream(message.Payload.ToArray());
        using var outputStream = new MemoryStream();
        using (var lz4Stream = LZ4Stream.Decode(inputStream, leaveOpen: true))
        {
            await lz4Stream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
