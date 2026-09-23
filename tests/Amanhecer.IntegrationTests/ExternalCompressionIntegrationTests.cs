using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Compression.LZ4;
using Amanhecer.Compression.Snappier;
using Amanhecer.Compression.Zstd;
using Amanhecer.Configurator;
using Amanhecer.InMemory;
using Amanhecer.Messaging;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Microsoft.Extensions.DependencyInjection;
using Snappier;
using ZstdSharp;

namespace Amanhecer.IntegrationTests;

public class ExternalCompressionIntegrationTests : BaseTests
{
    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<ProcessedRequests>();
    }

    protected override void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<Lz4RequestHandler>()
            .AddRequestHandler<SnappierRequestHandler>()
            .AddRequestHandler<ZstdRequestHandler>()
            .UsingMessagingGateway(messaging => messaging
                .DefaultMessageMapper<JsonMessageMapper>()
                .UsingInMemory(inMemory => inMemory
                    .Publications(publications =>
                    {
                        publications.AddPublication(publication => publication
                            .Name("lz4-publication")
                            .RoutingKey("tests.lz4")
                            .QueueName("tests.lz4.queue")
                            .Transformer<Lz4Compress>(metadata: new Lz4Metadata(LZ4Level.L09_HC)));

                        publications.AddPublication(publication => publication
                            .Name("snappier-publication")
                            .RoutingKey("tests.snappier")
                            .QueueName("tests.snappier.queue")
                            .Transformer<SnappierCompress>(metadata: new SnappierMetadata()));

                        publications.AddPublication(publication => publication
                            .Name("zstd-publication")
                            .RoutingKey("tests.zstd")
                            .QueueName("tests.zstd.queue")
                            .Transformer<ZstdCompress>(metadata: new ZstdMetadata(5)));
                    })
                    .Subscriptions(subscriptions =>
                    {
                        subscriptions.AddSubscription(subscription => subscription
                            .Name("lz4-subscription")
                            .ToRoutingKey("tests.lz4")
                            .QueueName("tests.lz4.queue")
                            .Transformer<Lz4Compress>(metadata: new Lz4Metadata(LZ4Level.L09_HC)));

                        subscriptions.AddSubscription(subscription => subscription
                            .Name("snappier-subscription")
                            .ToRoutingKey("tests.snappier")
                            .QueueName("tests.snappier.queue")
                            .Transformer<SnappierCompress>(metadata: new SnappierMetadata()));

                        subscriptions.AddSubscription(subscription => subscription
                            .Name("zstd-subscription")
                            .ToRoutingKey("tests.zstd")
                            .QueueName("tests.zstd.queue")
                            .Transformer<ZstdCompress>(metadata: new ZstdMetadata(5)));
                    })));
    }

    [Test]
    public async Task When_Lz4Transformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new Lz4Request("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.lz4",
            Lz4Decompress,
            async r => await Assert.That(r).IsTypeOf<Lz4Request>().And.IsEqualTo(request));
    }

    [Test]
    public async Task When_SnappierTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new SnappierRequest("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.snappier",
            SnappierDecompress,
            async r => await Assert.That(r).IsTypeOf<SnappierRequest>().And.IsEqualTo(request));
    }

    [Test]
    public async Task When_ZstdTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new ZstdRequest("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.zstd",
            ZstdDecompress,
            async r => await Assert.That(r).IsTypeOf<ZstdRequest>().And.IsEqualTo(request));
    }

    private async Task AssertRoundTripAsync<TRequest>(
        TRequest request,
        string routingKey,
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> decompress,
        Func<object, Task> expected)
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();
        var gateway = (InMemoryGateway)ServiceProvider.GetServices<IGateway>().Single();
        var subscription = gateway.Subscriptions.Single(x => x.ToRoutingKey == routingKey);
        var consumer = gateway.CreateConsumer(subscription);
        var processed = ServiceProvider.GetRequiredService<ProcessedRequests>();
        var originalPayload = JsonSerializer.SerializeToUtf8Bytes(request);

        await dispatcher.PostAsync(request!);

        using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await consumer.GetMessagesAsync(receiveCts.Token);
        await Assert.That(received).Count().IsEqualTo(1);
        await Assert.That(received[0].Payload.Span.SequenceEqual(originalPayload)).IsFalse();
        await Assert.That(decompress(received[0].Payload).ToArray()).IsEquivalentTo(originalPayload);

        await consumer.DeferAsync(received[0], TimeSpan.Zero);

        var pump = ServiceProvider.GetRequiredService<IMessagePump>();
        using var pumpCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pumpTask = pump.ExecuteAsync(consumer, pumpCts.Token);

        var handled = await processed.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pumpCts.Cancel();
        try
        {
            await pumpTask;
        }
        catch (OperationCanceledException)
        {
        }

        await expected(handled);
        processed.Reset();
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

    [RoutingKey("tests.lz4")]
    private sealed record Lz4Request(string Text);

    [RoutingKey("tests.snappier")]
    private sealed record SnappierRequest(string Text);

    [RoutingKey("tests.zstd")]
    private sealed record ZstdRequest(string Text);

    private sealed class ProcessedRequests
    {
        public TaskCompletionSource<object> Completion { get; private set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(object request)
        {
            Completion.TrySetResult(request);
        }

        public void Reset()
        {
            Completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class Lz4RequestHandler(ProcessedRequests processed) : RequestHandler<Lz4Request>
    {
        public override ValueTask HandleAsync(Lz4Request request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SnappierRequestHandler(ProcessedRequests processed) : RequestHandler<SnappierRequest>
    {
        public override ValueTask HandleAsync(SnappierRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ZstdRequestHandler(ProcessedRequests processed) : RequestHandler<ZstdRequest>
    {
        public override ValueTask HandleAsync(ZstdRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }
}
