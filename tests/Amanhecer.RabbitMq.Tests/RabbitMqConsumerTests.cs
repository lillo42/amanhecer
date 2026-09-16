using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for <see cref="RabbitMqConsumer"/> settlement and
/// <see cref="RabbitMqMessagePoller"/> delivery mapping, backed by a mocked
/// <see cref="IChannel"/>.
/// </summary>
public class RabbitMqConsumerTests
{
    [Test]
    public async Task When_Deferring_A_Message_Below_The_Max_Delivery_Attempts_Should_Requeue()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue") { MaxDeliveryAttempts = 3 };
        var consumer = new RabbitMqConsumer(new RabbitMqMessagePoller(subscription, channel), subscription);

        await consumer.DeferAsync(CreateMessage(deliveryAttempts: 2), TimeSpan.FromSeconds(5));

        await channel.Received(1).BasicNackAsync(1ul, false, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Deferring_A_Message_At_The_Max_Delivery_Attempts_Should_Not_Requeue()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue") { MaxDeliveryAttempts = 3 };
        var consumer = new RabbitMqConsumer(new RabbitMqMessagePoller(subscription, channel), subscription);

        await consumer.DeferAsync(CreateMessage(deliveryAttempts: 3), TimeSpan.FromSeconds(5));

        await channel.Received(1).BasicNackAsync(1ul, false, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_A_Message_Is_Delivered_Should_Count_The_Delivery_Attempts()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var poller = new RabbitMqMessagePoller(subscription, channel);
        var properties = Substitute.For<IReadOnlyBasicProperties>();
        properties.MessageId.Returns("tests.message");

        await DeliverAsync(poller, properties, deliveryTag: 1);
        var first = await poller.Messages.ReadAsync(CancellationToken.None);
        await Assert.That(first.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(1);

        await DeliverAsync(poller, properties, deliveryTag: 2);
        var second = await poller.Messages.ReadAsync(CancellationToken.None);
        await Assert.That(second.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(2);
    }

    [Test]
    public async Task When_The_Delivery_Carries_Dead_Letter_Counts_Should_Count_Them_As_Attempts()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var poller = new RabbitMqMessagePoller(subscription, channel);
        var properties = Substitute.For<IReadOnlyBasicProperties>();
        properties.MessageId.Returns("tests.message");
        properties.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-death"] = new List<object>
            {
                new Dictionary<string, object?> { ["count"] = 3L }
            }
        });

        await DeliverAsync(poller, properties, deliveryTag: 1);
        var message = await poller.Messages.ReadAsync(CancellationToken.None);

        await Assert.That(message.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(4);
    }

    [Test]
    public async Task When_The_Delivery_Carries_A_Quorum_Delivery_Count_Should_Count_It_As_An_Attempt()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue") { MaxDeliveryAttempts = 5 };
        var poller = new RabbitMqMessagePoller(subscription, channel);
        var properties = Substitute.For<IReadOnlyBasicProperties>();
        properties.MessageId.Returns("tests.message");
        properties.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-delivery-count"] = 7L
        });

        await DeliverAsync(poller, properties, deliveryTag: 1);
        var message = await poller.Messages.ReadAsync(CancellationToken.None);

        // The broker delivered the message 8 times: over the configured cap of 5, so
        // deferring nacks it without requeue even though the client never saw it before.
        await Assert.That(message.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(8);

        var consumer = new RabbitMqConsumer(poller, subscription);
        await consumer.DeferAsync(message, TimeSpan.FromSeconds(5));

        await channel.Received(1).BasicNackAsync(1ul, false, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_The_Delivery_Count_Arrives_As_Int_Or_Bytes_Should_Count_It_As_An_Attempt()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var poller = new RabbitMqMessagePoller(subscription, channel);

        var intProperties = Substitute.For<IReadOnlyBasicProperties>();
        intProperties.MessageId.Returns("tests.int");
        intProperties.Headers.Returns(new Dictionary<string, object?> { ["x-delivery-count"] = 2 });

        await DeliverAsync(poller, intProperties, deliveryTag: 1);
        var fromInt = await poller.Messages.ReadAsync(CancellationToken.None);
        await Assert.That(fromInt.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(3);

        var bytesProperties = Substitute.For<IReadOnlyBasicProperties>();
        bytesProperties.MessageId.Returns("tests.bytes");
        bytesProperties.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-delivery-count"] = "3"u8.ToArray()
        });

        await DeliverAsync(poller, bytesProperties, deliveryTag: 2);
        var fromBytes = await poller.Messages.ReadAsync(CancellationToken.None);
        await Assert.That(fromBytes.Metadata[MetadataName.DeliveryAttempts]).IsEqualTo(4);
    }

    [Test]
    public async Task When_The_Max_Delivery_Attempts_Is_Disabled_Should_Always_Requeue()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue") { MaxDeliveryAttempts = 0 };
        var consumer = new RabbitMqConsumer(new RabbitMqMessagePoller(subscription, channel), subscription);

        await consumer.DeferAsync(CreateMessage(deliveryAttempts: 100), TimeSpan.FromSeconds(5));

        await channel.Received(1).BasicNackAsync(1ul, false, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_The_Handler_Cancellation_Fires_While_Buffering_Should_Requeue_The_Delivery()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var poller = new RabbitMqMessagePoller(subscription, channel);
        var properties = Substitute.For<IReadOnlyBasicProperties>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await DeliverAsync(poller, properties, deliveryTag: 7, cts.Token);

        await channel.Received(1).BasicNackAsync(7ul, false, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_The_Message_Carries_A_CloudEvents_Time_Should_Parse_It_As_Invariant()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var poller = new RabbitMqMessagePoller(subscription, channel);
        var time = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var properties = Substitute.For<IReadOnlyBasicProperties>();
        properties.Headers.Returns(new Dictionary<string, object?>
        {
            ["cloudEvents:time"] = time.ToString("O", CultureInfo.InvariantCulture)
        });

        var previousCulture = CultureInfo.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            await DeliverAsync(poller, properties, deliveryTag: 1);
            var message = await poller.Messages.ReadAsync(CancellationToken.None);

            await Assert.That(message.Time).IsEqualTo(time);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task When_The_Max_Delivery_Attempts_Is_Not_Set_Should_Log_A_Warning()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue");
        var logger = new TestLogger();

        _ = new RabbitMqConsumer(new RabbitMqMessagePoller(subscription, channel), subscription, logger);

        await Assert.That(logger.Warnings.Count).IsEqualTo(1);
        await Assert.That(logger.Warnings[0].Contains("tests.queue")).IsTrue();
    }

    [Test]
    public async Task When_The_Max_Delivery_Attempts_Is_Set_Should_Not_Log_A_Warning()
    {
        var channel = Substitute.For<IChannel>();
        var subscription = new RabbitMqSubscription("tests", "tests.queue") { MaxDeliveryAttempts = 3 };
        var logger = new TestLogger();

        _ = new RabbitMqConsumer(new RabbitMqMessagePoller(subscription, channel), subscription, logger);

        await Assert.That(logger.Warnings).IsEmpty();
    }

    private sealed class TestLogger : ILogger<RabbitMqConsumer>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private static Task DeliverAsync(RabbitMqMessagePoller poller,
        IReadOnlyBasicProperties properties,
        ulong deliveryTag,
        CancellationToken cancellationToken = default)
    {
        return poller.HandleBasicDeliverAsync("tests.consumer",
            deliveryTag,
            redelivered: false,
            "tests.exchange",
            "tests",
            properties,
            ReadOnlyMemory<byte>.Empty,
            cancellationToken);
    }

    private static Message CreateMessage(ulong deliveryTag = 1, int? deliveryAttempts = null)
    {
        var metadata = new Dictionary<string, object?>
        {
            [MetadataName.DeliveryTag] = deliveryTag
        };
        if (deliveryAttempts != null)
        {
            metadata[MetadataName.DeliveryAttempts] = deliveryAttempts.Value;
        }

        return new Message
        {
            Metadata = metadata,
            Payload = ReadOnlyMemory<byte>.Empty
        };
    }
}
