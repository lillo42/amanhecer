#if !NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Compression;

/// <summary>
/// Compression settings used by <see cref="ZipCompress"/>.
/// </summary>
/// <param name="CompressionLevel">The ZIP entry compression level applied when encoding.</param>
/// <param name="EntryName">The entry name used inside the ZIP archive.</param>
public record ZipMetadata(CompressionLevel CompressionLevel, string EntryName = "payload");

/// <summary>
/// Declares that <see cref="ZipCompress"/> applies to the messages mapped by the message mapper
/// type the attribute is placed on, using the configured ZIP entry compression level.
/// </summary>
/// <param name="compressionLevel">The ZIP entry compression level applied when encoding.</param>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class ZipAttribute(CompressionLevel compressionLevel, int order) : TransformerAttribute<ZipCompress>(order)
{
    /// <summary>
    /// Gets the ZIP entry compression level applied when encoding.
    /// </summary>
    public CompressionLevel CompressionLevel => compressionLevel;

    /// <summary>
    /// Gets the metadata exposed to the transformer pipeline.
    /// </summary>
    public override object Metadata => new ZipMetadata(compressionLevel);
}

/// <summary>
/// An <see cref="ITransformer"/> that compresses outgoing message payloads into a single-entry
/// ZIP archive and decompresses incoming payloads that were encoded with the same transformer.
/// </summary>
public class ZipCompress : ITransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetRequiredMetadata<ZipMetadata>();

        using var outputStream = new MemoryStream();

#if NET10_0_OR_GREATER
        await using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
#else
        using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
#endif
        {
            var entry = archive.CreateEntry(metadata.EntryName, metadata.CompressionLevel);

#if NET10_0_OR_GREATER
            await using var entryStream = await entry.OpenAsync();
            await entryStream.WriteAsync(message.Payload)
                .ConfigureAwait(context.ContinueOnCapturedContext);
#elif NETSTANDARD
            var payload = message.Payload.ToArray();
            using var entryStream = entry.Open();
            await entryStream.WriteAsync(payload, 0, payload.Length)
                .ConfigureAwait(context.ContinueOnCapturedContext);
#else
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(message.Payload)
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
#if NET10_0_OR_GREATER
        await using (var archive = new ZipArchive(inputStream, ZipArchiveMode.Read, leaveOpen: true))
#else
        using (var archive = new ZipArchive(inputStream, ZipArchiveMode.Read, leaveOpen: true))
#endif
        {
            var entry = archive.Entries.FirstOrDefault();
            if (entry == null)
            {
                throw new FormatException("The ZIP payload must contain at least one entry.");
            }

#if NET10_0_OR_GREATER
            await using var entryStream = await entry.OpenAsync();
#elif NETSTANDARD
            using var entryStream = entry.Open();
#else
            await using var entryStream = entry.Open();
#endif
            await entryStream.CopyToAsync(outputStream).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        message.Payload = outputStream.ToArray();
        await next(message, context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
#endif