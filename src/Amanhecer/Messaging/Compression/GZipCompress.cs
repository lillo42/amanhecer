using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Compression;

/// <summary>
/// Compression settings used by <see cref="GZipCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The GZip compression level applied when encoding.</param>
public record GZipMetadata(CompressionLevel CompressionLevel);

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
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new GZipMetadata(compressionLevel);
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
        var compressionLevel = context.GetRequiredMetadata<GZipMetadata>().CompressionLevel;
        using var outputStream = new MemoryStream();

#if NET8_0_OR_GREATER
        await using (var gzipStream = new GZipStream(outputStream, compressionLevel, leaveOpen: true))
        {
            await gzipStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#else
        using (var gzipStream = new GZipStream(outputStream, compressionLevel, leaveOpen: true))
        {
            var payload = message.Payload.ToArray();
            await gzipStream.WriteAsync(payload, 0, payload.Length)
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
        await using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
#else
        using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
#endif
        {
            await gzipStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}