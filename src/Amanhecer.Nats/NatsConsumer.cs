using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using NATS.Client.Core;
using NATS.Net;

namespace Amanhecer.Nats;

public class NatsConsumer(
    NatsSubscription subscription,
    NatsClient client,
    IAsyncEnumerable<NatsMsg<byte[]>> consumer) : IConsumer, IAsyncDisposable
{
    private Task? _consumerTask;
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly Channel<Message> _channel =
        Channel.CreateBounded<Message>(new BoundedChannelOptions(subscription.BufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

    public ISubscription Subscription => subscription;

    public void Start()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
        }

        if (_consumerTask != null)
        {
#if !NETSTANDARD2_0
            _consumerTask.GetAwaiter().GetResult();
#endif
            _consumerTask = null;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _consumerTask = ConsumerAsync(_channel.Writer, _cancellationTokenSource.Token);
    }

    private async Task ConsumerAsync(ChannelWriter<Message> writer, CancellationToken cancellationToken)
    {
        await Task.Yield();
        await foreach (var natsMessage in consumer.WithCancellation(cancellationToken))
        {
            var message = ToMessage(natsMessage);
            await writer.WriteAsync(message, cancellationToken);
            await writer.WaitToWriteAsync(cancellationToken);
        }
    }

    private static Message ToMessage(NatsMsg<byte[]> natsMessage)
    {
        var message = new Message
        {
            Payload = natsMessage.Data,
            ReplyTo = natsMessage.ReplyTo
        };

        if (natsMessage.Headers != null)
        {
            foreach (var header in natsMessage.Headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }
        }

        message.Metadata["NatsMessage"] = natsMessage;

        return message;
    }

    public ValueTask AckAsync(Message message)
    {
        return new ValueTask();
    }

    public ValueTask NackAsync(Message message)
    {
        return new ValueTask();
    }

    public async ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (!message.Metadata.TryGetValue("NatsMessage", out var obj)
            || obj is not NatsMsg<byte[]> natsMessage)
        {
            return;
        }

        await client.PublishAsync(
            natsMessage.Subject,
            natsMessage.Data,
            natsMessage.Headers,
            natsMessage.ReplyTo);
    }

    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new List<Message>(subscription.BufferSize);

        var reader = _channel.Reader;
        if (await reader.WaitToReadAsync(cancellationToken))
        {
            while (!cancellationToken.IsCancellationRequested
                   && buffer.Count < subscription.BufferSize
                   && reader.TryRead(out var message))
            {
                buffer.Add(message);
            }
        }

        return [.. buffer];
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource != null)
        {
#if NETSTANDARD2_0
            _cancellationTokenSource.Cancel();
#else
            await _cancellationTokenSource.CancelAsync();
#endif
            await CastAndDispose(_cancellationTokenSource);
            
            _cancellationTokenSource = null;
        }

        if (_consumerTask != null)
        {
#if !NETSTANDARD2_0
            await _consumerTask;
#endif
            await CastAndDispose(_consumerTask);
            _consumerTask = null;
        }

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}