using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Compression;

/// <summary>
/// Compression settings used by <see cref="DeflateCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The Deflate compression level applied when encoding.</param>
public record DeflateMetadata(CompressionLevel CompressionLevel);

/// <summary>
/// Declares that <see cref="DeflateCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on, using the configured Deflate compression level.
/// </summary>
/// <param name="compressionLevel">The Deflate compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class DeflateAttribute(CompressionLevel compressionLevel, int order) : TransformerAttribute<DeflateCompress>(order)
{
    /// <summary>
    /// Gets the Deflate compression level applied when encoding.
    /// </summary>
    public CompressionLevel CompressionLevel => compressionLevel;

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new DeflateMetadata(compressionLevel);
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
        var compressionLevel = context.GetRequiredMetadata<DeflateMetadata>().CompressionLevel;
        using var outputStream = new MemoryStream();
        
#if NET8_0_OR_GREATER
        await using (var deflateStream = new DeflateStream(outputStream, compressionLevel, leaveOpen: true))
        {
            await deflateStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#else
        using (var deflateStream = new DeflateStream(outputStream, compressionLevel, leaveOpen: true))
        {
            var payload = message.Payload.ToArray();
            await deflateStream.WriteAsync(payload, 0, payload.Length)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#endif

        message.Payload = outputStream.ToArray();

        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        using var inputStream = new MemoryStream(message.Payload.ToArray());
        using var outputStream = new MemoryStream();
        
#if NET8_0_OR_GREATER
        await using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
#else
        using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
#endif
        {
            await deflateStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
