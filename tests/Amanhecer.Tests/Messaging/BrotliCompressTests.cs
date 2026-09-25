using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Compression;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class BrotliCompressTests
{
    private readonly BrotliCompress _transformer = new();

    [Test]
    public async Task EncodeAsync_With_BrotliMetadata_Should_Compress_The_Payload()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = payload };
        var context = new AmanhecerContext();
        context.SetMetadata(new BrotliMetadata(CompressionLevel.Optimal));
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.Payload.Span.SequenceEqual(payload)).IsFalse();
        await Assert.That(Decompress(message.Payload).ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Compressed_Payload_Should_Decompress_It()
    {
        var payload = "hello hello hello hello hello"u8.ToArray();
        var message = new Message { Payload = Compress(payload) };
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        var context = new AmanhecerContext();
        context.SetMetadata(new BrotliMetadata(CompressionLevel.Optimal) { ShouldDecompress = _ => true });
        await _transformer.DecodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await next.Received(1).Invoke(message, context);
    }

    private static ReadOnlyMemory<byte> Compress(ReadOnlyMemory<byte> payload)
    {
        using var output = new MemoryStream();
        using (var stream = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            stream.Write(payload.Span);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> Decompress(ReadOnlyMemory<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var output = new MemoryStream();
        using (var stream = new BrotliStream(input, CompressionMode.Decompress))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }
}
