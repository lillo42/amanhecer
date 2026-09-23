using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Snappier;

namespace Amanhecer.Compression.Snappier;

/// <summary>
/// Marker metadata used by <see cref="SnappierCompress"/>.
/// </summary>
public record SnappierMetadata;

/// <summary>
/// Declares that <see cref="SnappierCompress"/> applies to the messages mapped by the message
/// mapper type the attribute is placed on.
/// </summary>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class SnappierAttribute(int order) : TransformerAttribute<SnappierCompress>(order)
{
    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new SnappierMetadata();
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
        _ = context.GetRequiredMetadata<SnappierMetadata>();

        using var outputStream = new MemoryStream();
#if NET8_0_OR_GREATER
        await using (var snappyStream = new SnappyStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            await snappyStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
#else
        using (var snappyStream = new SnappyStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            var payload = message.Payload.ToArray();
            await snappyStream.WriteAsync(payload, 0, payload.Length)
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
        await using (var snappyStream = new SnappyStream(inputStream, CompressionMode.Decompress, leaveOpen: true))
#else
        using (var snappyStream = new SnappyStream(inputStream, CompressionMode.Decompress, leaveOpen: true))
#endif
        {
            await snappyStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}