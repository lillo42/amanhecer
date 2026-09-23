using System;
using System.IO;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using ZstdSharp;

namespace Amanhecer.Compression.Zstd;

/// <summary>
/// Compression settings used by <see cref="ZstdCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The Zstandard compression level applied when encoding.</param>
public record ZstdMetadata(int CompressionLevel);

/// <summary>
/// Declares that <see cref="ZstdCompress"/> applies to the messages mapped by the message mapper
/// type the attribute is placed on, using the configured Zstandard compression level.
/// </summary>
/// <param name="compressionLevel">The Zstandard compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class ZstdAttribute(int compressionLevel, int order) : TransformerAttribute<ZstdCompress>(order)
{
    /// <summary>
    /// Gets the Zstandard compression level applied when encoding.
    /// </summary>
    public int CompressionLevel => compressionLevel;

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new ZstdMetadata(compressionLevel);
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with Zstandard and
/// decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class ZstdCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<ZstdMetadata>();

        using var outputStream = new MemoryStream();
        await using (var zstdStream = new CompressionStream(outputStream, metadata.CompressionLevel, leaveOpen: true))
        {
#if NET8_0_OR_GREATER
            await zstdStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
#else
            var payload = message.Payload.ToArray();
            await zstdStream.WriteAsync(payload, 0, payload.Length)
                .ConfigureAwait(context.ContinueOnCapturedContext);
#endif
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
        
        await using (var zstdStream = new DecompressionStream(inputStream, leaveOpen: true))
        {
            await zstdStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
