using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Compression;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AdditionalCompressionTests
{
    [Test]
    public async Task EncodeAsync_With_GZipMetadata_Should_Compress_The_Payload()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new GZipMetadata(CompressionLevel.Optimal));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new GZipCompress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(GZipDecompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_GZip_Compressed_Payload_Should_Decompress_It()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = GZipCompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        var context = new AmanhecerContext();
        context.SetMetadata(new GZipMetadata(CompressionLevel.Optimal) { ShouldDecompress = _ => true });
        await new GZipCompress().DecodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task EncodeAsync_With_DeflateMetadata_Should_Compress_The_Payload()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new DeflateMetadata(CompressionLevel.Optimal));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new DeflateCompress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(DeflateDecompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Deflate_Compressed_Payload_Should_Decompress_It()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = DeflateCompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        var context = new AmanhecerContext();
        context.SetMetadata(new DeflateMetadata(CompressionLevel.Optimal) { ShouldDecompress = _ => true });
        await new DeflateCompress().DecodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }

#if !NETFRAMEWORK
    [Test]
    public async Task EncodeAsync_With_ZipMetadata_Should_Compress_The_Payload()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new ZipMetadata(CompressionLevel.Optimal, "payload.bin"));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new ZipCompress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(ZipDecompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Zip_Compressed_Payload_Should_Decompress_It()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = ZipCompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        var context = new AmanhecerContext();
        context.SetMetadata(new ZipMetadata(CompressionLevel.Optimal) { ShouldDecompress = _ => true });
        await new ZipCompress().DecodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }
#endif

    private static ReadOnlyMemory<byte> GZipCompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> GZipDecompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = new GZipStream(input, CompressionMode.Decompress))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> DeflateCompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> DeflateDecompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = new DeflateStream(input, CompressionMode.Decompress))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }

#if !NETFRAMEWORK
    private static ReadOnlyMemory<byte> ZipCompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("payload", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> ZipDecompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var entryStream = archive.Entries.Single().Open();
            entryStream.CopyTo(output);
        }

        return output.ToArray();
    }
#endif
}
