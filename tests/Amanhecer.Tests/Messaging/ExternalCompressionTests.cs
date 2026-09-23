using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Compression.LZ4;
using Amanhecer.Compression.Snappier;
using Amanhecer.Compression.Zstd;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using NSubstitute;
using Snappier;
using ZstdSharp;

namespace Amanhecer.Tests.Messaging;

public class ExternalCompressionTests
{
    [Test]
    public async Task EncodeAsync_With_Lz4Metadata_Should_Compress_The_Payload()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new Lz4Metadata(LZ4Level.L09_HC));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new Lz4Compress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(Lz4Decompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Lz4_Compressed_Payload_Should_Decompress_It()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = Lz4CompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new Lz4Compress().DecodeAsync(message, new AmanhecerContext(), next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task EncodeAsync_With_SnappierMetadata_Should_Compress_The_Payload()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new SnappierMetadata());
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new SnappierCompress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(SnappierDecompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Snappier_Compressed_Payload_Should_Decompress_It()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = SnappierCompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new SnappierCompress().DecodeAsync(message, new AmanhecerContext(), next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task EncodeAsync_With_ZstdMetadata_Should_Compress_The_Payload()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new ZstdMetadata(5));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new ZstdCompress().EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(ZstdDecompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Zstd_Compressed_Payload_Should_Decompress_It()
    {
        var payload = Encoding.UTF8.GetBytes("hello hello hello hello hello");
        var message = new Message { Payload = ZstdCompressPayload(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await new ZstdCompress().DecodeAsync(message, new AmanhecerContext(), next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, Arg.Any<AmanhecerContext>());
    }

    private static ReadOnlyMemory<byte> Lz4CompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = LZ4Stream.Encode(output,
                   new LZ4EncoderSettings { CompressionLevel = LZ4Level.L09_HC },
                   leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> Lz4Decompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = LZ4Stream.Decode(input, leaveOpen: true))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> SnappierCompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = new SnappyStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> SnappierDecompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = new SnappyStream(input, CompressionMode.Decompress, leaveOpen: true))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> ZstdCompressPayload(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = new CompressionStream(output, 5, leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> ZstdDecompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = new DecompressionStream(input, leaveOpen: true))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }
}
