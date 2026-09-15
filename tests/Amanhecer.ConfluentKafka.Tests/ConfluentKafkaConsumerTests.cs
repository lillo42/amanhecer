using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.ConfluentKafka;
using Confluent.Kafka;
using NSubstitute;

namespace Amanhecer.ConfluentKafka.Tests;

/// <summary>
/// Broker-free tests for <see cref="ConfluentKafkaConsumer"/>: message mapping, offset
/// store/commit behavior (batch, sweeper, revoke, close), the fatal-error latch and
/// seek-back deferral, over a substituted consumer client.
/// </summary>
public class ConfluentKafkaConsumerTests
{
    private const string TopicName = "tests.topic";

    private static ConfluentKafkaSubscription CreateSubscription()
    {
        return new ConfluentKafkaSubscription("tests", TopicName, "tests-group")
        {
            CommitBatchSize = 10,
            SweepUncommittedOffsetsInterval = TimeSpan.FromHours(1)
        };
    }

    private static ConsumeResult<string?, byte[]> CreateConsumeResult(long offset,
        string? key = "key",
        Dictionary<string, string>? headers = null,
        DateTime? timestamp = null)
    {
        var kafkaHeaders = new Headers();
        foreach (var header in headers ?? [])
        {
            kafkaHeaders.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
        }

        return new ConsumeResult<string?, byte[]>
        {
            Topic = TopicName,
            Partition = 0,
            Offset = offset,
            TopicPartitionOffset = new TopicPartitionOffset(TopicName, 0, offset),
            Message = new Message<string?, byte[]>
            {
                Key = key,
                Value = Encoding.UTF8.GetBytes("payload"),
                Headers = kafkaHeaders,
                Timestamp = new Timestamp(timestamp ?? DateTime.UtcNow)
            }
        };
    }

    private static Message CreateMessage(long offset)
    {
        return new Message
        {
            Payload = Encoding.UTF8.GetBytes("payload"),
            Metadata =
            {
                [MetadataName.TopicPartitionOffset] = new TopicPartitionOffset(TopicName, 0, offset)
            }
        };
    }

    private static CancellationToken Token()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private static bool CommitWasCalled(IConsumer<string?, byte[]> consumer)
    {
        return consumer.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IConsumer<string?, byte[]>.Commit));
    }

    [Test]
    public async Task GetMessagesAsync_Should_Map_ConsumeResult_To_Message()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var consumeResult = CreateConsumeResult(41, headers: new Dictionary<string, string>
        {
            ["ce_id"] = "message-id",
            ["ce_type"] = "tests.message",
            ["ce_correlationid"] = "correlation-id",
            ["ce_time"] = "2026-09-15T10:20:30.0000000+00:00",
            ["custom"] = "custom-value"
        });
        kafkaConsumer.Consume(Arg.Any<CancellationToken>()).Returns(consumeResult);

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages).Count().IsEqualTo(1);
        var message = messages[0];
        await Assert.That(message.Id).IsEqualTo("message-id");
        await Assert.That(message.Type).IsEqualTo("tests.message");
        await Assert.That(message.CorrelationId).IsEqualTo("correlation-id");
        await Assert.That(message.Time).IsEqualTo(new DateTimeOffset(2026, 9, 15, 10, 20, 30, TimeSpan.Zero));
        await Assert.That(message.PartitionKey).IsEqualTo("key");
        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(Encoding.UTF8.GetBytes("payload"));
        await Assert.That(message.Headers["custom"]).IsTypeOf<byte[]>();
        await Assert.That(Encoding.UTF8.GetString((byte[])message.Headers["custom"]!)).IsEqualTo("custom-value");
        await Assert.That(message.Metadata[MetadataName.Offset]).IsEqualTo(41L);
    }

    [Test]
    public async Task GetMessagesAsync_Without_CeTime_Should_Use_Record_Timestamp()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var recordTime = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        kafkaConsumer.Consume(Arg.Any<CancellationToken>())
            .Returns(CreateConsumeResult(41, timestamp: recordTime));

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages[0].Time).IsEqualTo(new DateTimeOffset(recordTime));
    }

    [Test]
    public async Task GetMessagesAsync_Without_Key_Should_Have_Null_PartitionKey()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        kafkaConsumer.Consume(Arg.Any<CancellationToken>()).Returns(CreateConsumeResult(41, key: null));

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages[0].PartitionKey).IsNull();
    }

    [Test]
    public async Task When_Acking_Should_Commit_Offset_When_Batch_Size_Is_Reached()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 1;
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Topic == TopicName
            && offsets.Single().Partition.Value == 0
            && offsets.Single().Offset.Value == 42));
    }

    [Test]
    public async Task When_Acking_Below_Batch_Size_Should_Commit_When_Batch_Completes()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 2;
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));
        await consumer.AckAsync(CreateMessage(42));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Count() == 2
            && offsets.Any(o => o.Offset.Value == 42)
            && offsets.Any(o => o.Offset.Value == 43)));
    }

    [Test]
    public async Task When_Sweep_Interval_Elapses_Should_Commit_Uncommitted_Offsets()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 100;
        subscription.SweepUncommittedOffsetsInterval = TimeSpan.FromMilliseconds(50);
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 42));
    }

    [Test]
    public async Task When_Disposing_Should_Commit_Outstanding_Offsets_And_Close()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 100;

        var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);
        await consumer.AckAsync(CreateMessage(41));

        consumer.Dispose();

        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 42));
        kafkaConsumer.Received(1).Close();
        kafkaConsumer.Received(1).Dispose();
    }

    [Test]
    public async Task When_Partitions_Are_Revoked_Should_Commit_Their_Stored_Offsets()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        var message = CreateMessage(41);
        await consumer.AckAsync(message);

        consumer.CommitOffsetsFor([new TopicPartitionOffset(TopicName, 0, 10)]);

        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 42));
    }

    [Test]
    public async Task When_A_Fatal_Error_Is_Raised_Should_Latch_And_Throw()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        consumer.HandleError(new Error(ErrorCode.Local_Fatal, "fatal", true));

        await Assert.That(async () => await consumer.GetMessagesAsync(Token()))
            .Throws<InvalidOperationException>();

        // A non-fatal error after a fatal one must not clear the latch.
        consumer.HandleError(new Error(ErrorCode.BrokerNotAvailable, "transient", false));

        await Assert.That(async () => await consumer.GetMessagesAsync(Token()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task When_Deferring_Should_Seek_Back_To_The_Message_Offset()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        await consumer.DeferAsync(CreateMessage(41), TimeSpan.Zero);

        kafkaConsumer.Received(1).Seek(Arg.Is<TopicPartitionOffset>(tpo =>
            tpo.Topic == TopicName && tpo.Partition.Value == 0 && tpo.Offset.Value == 41));
    }

    [Test]
    public async Task When_Deferring_With_Delay_Should_Pause_And_Resume_The_Partition()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        await consumer.DeferAsync(CreateMessage(41), TimeSpan.FromMilliseconds(50));

        kafkaConsumer.Received(1).Pause(Arg.Is<IEnumerable<TopicPartition>>(partitions =>
            partitions.Single() == new TopicPartition(TopicName, 0)));

        await UntilAsync(() =>
            kafkaConsumer.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IConsumer<string?, byte[]>.Resume)));
        kafkaConsumer.Received(1).Resume(Arg.Is<IEnumerable<TopicPartition>>(partitions =>
            partitions.Single() == new TopicPartition(TopicName, 0)));
    }
}
