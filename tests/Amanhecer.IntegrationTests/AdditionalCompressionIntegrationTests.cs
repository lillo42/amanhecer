using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.InMemory;
using Amanhecer.Messaging;
using Amanhecer.Messaging.Compression;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.IntegrationTests;

public class AdditionalCompressionIntegrationTests : BaseTests
{
    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<ProcessedRequests>();
    }

    protected override void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<GZipRequestHandler>()
            .AddRequestHandler<DeflateRequestHandler>()
#if !NETFRAMEWORK
            .AddRequestHandler<ZipRequestHandler>()
#endif
            .UsingMessagingGateway(messaging => messaging
                .DefaultMessageMapper<JsonMessageMapper>()
                .UsingInMemory(inMemory => inMemory
                    .Publications(publications =>
                    {
                        publications.AddPublication(publication => publication
                            .Name("gzip-publication")
                            .RoutingKey("tests.gzip")
                            .QueueName("tests.gzip.queue")
                            .Transformer<GZipCompress>(metadata: new GZipMetadata(CompressionLevel.Fastest)));

                        publications.AddPublication(publication => publication
                            .Name("deflate-publication")
                            .RoutingKey("tests.deflate")
                            .QueueName("tests.deflate.queue")
                            .Transformer<DeflateCompress>(metadata: new DeflateMetadata(CompressionLevel.Fastest)));

#if !NETFRAMEWORK
                        publications.AddPublication(publication => publication
                            .Name("zip-publication")
                            .RoutingKey("tests.zip")
                            .QueueName("tests.zip.queue")
                            .Transformer<ZipCompress>(metadata: new ZipMetadata(CompressionLevel.Fastest)));
#endif
                    })
                    .Subscriptions(subscriptions =>
                    {
                        subscriptions.AddSubscription(subscription => subscription
                            .Name("gzip-subscription")
                            .ToRoutingKey("tests.gzip")
                            .QueueName("tests.gzip.queue")
                            .Transformer<GZipCompress>(metadata: new GZipMetadata(CompressionLevel.Fastest)));

                        subscriptions.AddSubscription(subscription => subscription
                            .Name("deflate-subscription")
                            .ToRoutingKey("tests.deflate")
                            .QueueName("tests.deflate.queue")
                            .Transformer<DeflateCompress>(metadata: new DeflateMetadata(CompressionLevel.Fastest)));

#if !NETFRAMEWORK
                        subscriptions.AddSubscription(subscription => subscription
                            .Name("zip-subscription")
                            .ToRoutingKey("tests.zip")
                            .QueueName("tests.zip.queue")
                            .Transformer<ZipCompress>(metadata: new ZipMetadata(CompressionLevel.Fastest)));
#endif
                    })));
    }

    [Test]
    public async Task When_GZipTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new GZipRequest("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.gzip",
            GZipDecompress,
            expected: async r => await Assert.That(r).IsTypeOf<GZipRequest>().And.IsEqualTo(request));
    }

    [Test]
    public async Task When_DeflateTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new DeflateRequest("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.deflate",
            DeflateDecompress,
            expected: async r => await Assert.That(r).IsTypeOf<DeflateRequest>().And.IsEqualTo(request));
    }

#if !NETFRAMEWORK
    [Test]
    public async Task When_ZipTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var request = new ZipRequest("hello hello hello hello hello");
        await AssertRoundTripAsync(
            request,
            "tests.zip",
            ZipDecompress,
            expected: async r => await Assert.That(r).IsTypeOf<ZipRequest>().And.IsEqualTo(request));
    }
#endif

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

    [RoutingKey("tests.gzip")]
    private sealed record GZipRequest(string Text);

    [RoutingKey("tests.deflate")]
    private sealed record DeflateRequest(string Text);

#if !NETFRAMEWORK
    [RoutingKey("tests.zip")]
    private sealed record ZipRequest(string Text);
#endif

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

    private sealed class GZipRequestHandler(ProcessedRequests processed) : RequestHandler<GZipRequest>
    {
        public override ValueTask HandleAsync(GZipRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DeflateRequestHandler(ProcessedRequests processed) : RequestHandler<DeflateRequest>
    {
        public override ValueTask HandleAsync(DeflateRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }

#if !NETFRAMEWORK
    private sealed class ZipRequestHandler(ProcessedRequests processed) : RequestHandler<ZipRequest>
    {
        public override ValueTask HandleAsync(ZipRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Complete(request);
            return ValueTask.CompletedTask;
        }
    }
#endif
}
