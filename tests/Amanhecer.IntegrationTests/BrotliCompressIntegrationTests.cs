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

public class BrotliCompressIntegrationTests : BaseTests
{
    private const string RoutingKey = "tests.brotli";
    private const string QueueName = "tests.brotli.queue";

    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<ProcessedRequests>();
    }

    protected override void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<CompressedRequestHandler>()
            .UsingMessagingGateway(messaging => messaging
                .DefaultMessageMapper<JsonMessageMapper>()
                .UsingInMemory(inMemory => inMemory
                    .Publications(publications => publications.AddPublication(publication => publication
                        .Name("brotli-publication")
                        .RoutingKey(RoutingKey)
                        .QueueName(QueueName)
                        .Transformer<BrotliCompress>(metadata: new BrotliMetadata(CompressionLevel.Fastest))))
                    .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
                        .Name("brotli-subscription")
                        .ToRoutingKey(RoutingKey)
                        .QueueName(QueueName)
                        .Transformer<BrotliCompress>(metadata: new BrotliMetadata(CompressionLevel.Fastest))))));
    }

    [Test]
    public async Task When_BrotliTransformer_Is_Configured_Should_RoundTrip_Through_The_Messaging_Pipeline()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();
        var gateway = (InMemoryGateway)ServiceProvider.GetServices<IGateway>().Single();
        var subscription = gateway.Subscriptions.Single();
        var consumer = gateway.CreateConsumer(subscription);
        var processed = ServiceProvider.GetRequiredService<ProcessedRequests>();
        var request = new CompressedRequest("hello hello hello hello hello");
        var originalPayload = JsonSerializer.SerializeToUtf8Bytes(request);

        await dispatcher.PostAsync(request);

        using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await consumer.GetMessagesAsync(receiveCts.Token);
        await Assert.That(received).Count().IsEqualTo(1);
        await Assert.That(received[0].Payload.Span.SequenceEqual(originalPayload)).IsFalse();
        await Assert.That(Decompress(received[0].Payload).ToArray()).IsEquivalentTo(originalPayload);

        await consumer.DeferAsync(received[0], TimeSpan.Zero);

        var pump = ServiceProvider.GetRequiredService<IMessagePump>();
        using var pumpCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pumpTask = pump.ExecuteAsync(consumer, pumpCts.Token);

        var handled = await processed.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pumpCts.Cancel();
        await pumpTask;

        await Assert.That(handled).IsEqualTo(request);
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

    [RoutingKey(RoutingKey)]
    private sealed record CompressedRequest(string Text);

    private sealed class ProcessedRequests
    {
        public TaskCompletionSource<CompressedRequest> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CompressedRequestHandler(ProcessedRequests processed) : RequestHandler<CompressedRequest>
    {
        public override ValueTask HandleAsync(CompressedRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            processed.Completion.TrySetResult(request);
            return ValueTask.CompletedTask;
        }
    }
}
