using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
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
                Value = "payload"u8.ToArray(),
                Headers = kafkaHeaders,
                Timestamp = new Timestamp(timestamp ?? DateTime.UtcNow)
            }
        };
    }

    private static Message CreateMessage(long offset, int partition = 0)
    {
        return new Message
        {
            Payload = "payload"u8.ToArray(),
            Metadata =
            {
                [MetadataName.TopicPartitionOffset] = new TopicPartitionOffset(TopicName, partition, offset)
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
            ["Content-Encoding"] = "br",
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
        await Assert.That(message.ContentEncoding).IsEqualTo("br");
        await Assert.That(message.Time).IsEqualTo(new DateTimeOffset(2026, 9, 15, 10, 20, 30, TimeSpan.Zero));
        await Assert.That(message.PartitionKey).IsEqualTo("key");
        await Assert.That(message.Payload.ToArray()).IsEquivalentTo("payload"u8.ToArray());
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
    public async Task GetMessagesAsync_At_Partition_Eof_Should_Return_No_Messages()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        kafkaConsumer.Consume(Arg.Any<CancellationToken>()).Returns(new ConsumeResult<string?, byte[]>
        {
            Topic = TopicName,
            Partition = 0,
            Offset = 41,
            IsPartitionEOF = true
        });

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, CreateSubscription());

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages).IsEmpty();
    }

    [Test]
    public async Task GetMessagesAsync_With_A_Buffer_Size_Should_Return_What_Is_Already_Fetched()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.BufferSize = 3;

        kafkaConsumer.Consume(Arg.Any<CancellationToken>()).Returns(CreateConsumeResult(41));
        kafkaConsumer.Consume(TimeSpan.Zero).Returns(CreateConsumeResult(42), CreateConsumeResult(43));

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages).Count().IsEqualTo(3);
        await Assert.That(messages.Select(m => (long)m.Metadata[MetadataName.Offset]!).ToArray())
            .IsEquivalentTo(new[] { 41L, 42L, 43L });
    }

    [Test]
    public async Task GetMessagesAsync_With_A_Buffer_Size_Should_Stop_When_Nothing_Is_Fetched()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.BufferSize = 10;

        var drained = 0;
        kafkaConsumer.Consume(Arg.Any<CancellationToken>()).Returns(CreateConsumeResult(41));
        kafkaConsumer.Consume(TimeSpan.Zero).Returns(_ => drained++ == 0 ? CreateConsumeResult(42) : null);

        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        var messages = await consumer.GetMessagesAsync(Token());

        await Assert.That(messages).Count().IsEqualTo(2);
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
        await consumer.AckAsync(CreateMessage(0, partition: 1));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Count() == 2
            && offsets.Any(o => o.Partition.Value == 0 && o.Offset.Value == 42)
            && offsets.Any(o => o.Partition.Value == 1 && o.Offset.Value == 1)));
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

        await consumer.AckAsync(CreateMessage(41));

        // The revoke handler reports the position the client had read to, which is always ahead
        // of what has been acked: the offset acked here still has to be committed.
        consumer.CommitOffsetsFor([new TopicPartitionOffset(TopicName, 0, 100)]);

        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 42));
    }

    [Test]
    public async Task When_Partitions_Are_Revoked_Should_Not_Commit_Their_Offsets_Again()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 100;
        var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));
        consumer.CommitOffsetsFor([new TopicPartitionOffset(TopicName, 0, 100)]);

        // The partition belongs to another member of the group now, so closing must not commit
        // for it again.
        consumer.Dispose();

        kafkaConsumer.Received(1).Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 42));
    }

    [Test]
    public async Task When_Acking_Several_Offsets_Of_A_Partition_Should_Commit_Only_The_Highest()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 3;
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));
        await consumer.AckAsync(CreateMessage(42));
        await consumer.AckAsync(CreateMessage(43));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 44));
    }

    [Test]
    public async Task When_Acking_Behind_A_Committed_Offset_Should_Not_Commit_Backwards()
    {
        var kafkaConsumer = Substitute.For<IConsumer<string?, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 1;
        using var consumer = new ConfluentKafkaConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(50));
        await UntilAsync(() => CommitWasCalled(kafkaConsumer));

        // A redelivery settled after the higher offset was committed must not move the group's
        // committed offset back, which would replay everything in between.
        await consumer.AckAsync(CreateMessage(30));

        kafkaConsumer.Received(1).Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());
        kafkaConsumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
            offsets.Single().Offset.Value == 51));
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
