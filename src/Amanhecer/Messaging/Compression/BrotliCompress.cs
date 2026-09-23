#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Compression;

/// <summary>
/// Compression settings used by <see cref="BrotliCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The Brotli compression level applied when encoding.</param>
public record BrotliMetadata(CompressionLevel CompressionLevel);

/// <summary>
/// Declares that <see cref="BrotliCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on, using the configured Brotli compression level.
/// </summary>
/// <param name="compressionLevel">The Brotli compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class BrotliAttribute(CompressionLevel compressionLevel, int order) : TransformerAttribute<BrotliCompress>(order)
{
    /// <summary>
    /// Gets the Brotli compression level applied when encoding.
    /// </summary>
    public CompressionLevel CompressionLevel => compressionLevel;

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new BrotliMetadata(compressionLevel);
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads with Brotli and
/// decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class BrotliCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var compressionLevel = context.GetRequiredMetadata<BrotliMetadata>().CompressionLevel;

        using var outputStream = new MemoryStream();
        await using (var brotliStream = new BrotliStream(outputStream, compressionLevel, leaveOpen: true))
        {
            await brotliStream.WriteAsync(message.Payload).ConfigureAwait(context.ContinueOnCapturedContext);
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
        await using (var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress))
        {
            await brotliStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
#endif
