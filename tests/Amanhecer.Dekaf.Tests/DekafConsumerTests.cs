using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Serialization;
using NSubstitute;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Broker-free tests for <see cref="DekafConsumer"/>: message mapping, offset store/commit
/// behavior (batch, sweeper, close) and seek-back deferral, over a substituted consumer
/// client.
/// </summary>
public class DekafConsumerTests
{
    private const string TopicName = "tests.topic";

    private static DekafSubscription CreateSubscription()
    {
        return new DekafSubscription("tests", TopicName, "tests-group")
        {
            CommitBatchSize = 10,
            SweepUncommittedOffsetsInterval = TimeSpan.FromHours(1)
        };
    }

    private static ConsumeResult<string, byte[]> CreateConsumeResult(long offset,
        string? key = "key",
        Dictionary<string, string>? headers = null,
        long? timestampMs = null)
    {
        var headerList = (headers ?? [])
            .Select(h => new Header(h.Key, Encoding.UTF8.GetBytes(h.Value)))
            .ToList();

        return new ConsumeResult<string, byte[]>(
            topic: TopicName,
            partition: 0,
            offset: offset,
            keyData: key is null ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(key),
            isKeyNull: key is null,
            valueData: Encoding.UTF8.GetBytes("payload"),
            isValueNull: false,
            headers: headerList,
            timestampMs: timestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            timestampType: TimestampType.CreateTime,
            leaderEpoch: -1,
            keyDeserializer: Serializers.String,
            valueDeserializer: Serializers.ByteArray);
    }

    private static async IAsyncEnumerable<ConsumeResult<string, byte[]>> Stream(
        params ConsumeResult<string, byte[]>[] results)
    {
        foreach (var result in results)
        {
            yield return result;
            await Task.CompletedTask;
        }
    }

    private static Message CreateMessage(long offset, int partition = 0)
    {
        return new Message
        {
            Payload = Encoding.UTF8.GetBytes("payload"),
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

    private static bool CommitWasCalled(IKafkaConsumer<string, byte[]> consumer)
    {
        return consumer.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IKafkaConsumer<string, byte[]>.CommitAsync));
    }

    private static async Task<Message> MapToMessageAsync(ConsumeResult<string, byte[]> result)
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        await using var consumer = new DekafConsumer(kafkaConsumer, CreateSubscription());
        var toMessage = typeof(DekafConsumer).GetMethod("ToMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Message)toMessage.Invoke(consumer, [result])!;
    }

    [Test]
    public async Task GetMessagesAsync_Should_Map_ConsumeResult_To_Message()
    {
        var message = await MapToMessageAsync(CreateConsumeResult(41, headers: new Dictionary<string, string>
        {
            ["ce_id"] = "message-id",
            ["ce_type"] = "tests.message",
            ["ce_correlationid"] = "correlation-id",
            ["ce_time"] = "2026-09-15T10:20:30.0000000+00:00",
            ["custom"] = "custom-value"
        }));

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
        var recordTime = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
        var message = await MapToMessageAsync(
            CreateConsumeResult(41, timestampMs: recordTime.ToUnixTimeMilliseconds()));

        await Assert.That(message.Time).IsEqualTo(recordTime);
    }

    [Test]
    public async Task GetMessagesAsync_Without_Key_Should_Have_Null_PartitionKey()
    {
        var message = await MapToMessageAsync(CreateConsumeResult(41, key: null));

        await Assert.That(message.PartitionKey).IsNull();
    }

    [Test]
    public async Task When_Acking_Should_Commit_Offset_When_Batch_Size_Is_Reached()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 1;
        await using var consumer = new DekafConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
                offsets.Single().Topic == TopicName
                && offsets.Single().Partition == 0
                && offsets.Single().Offset == 42),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Acking_Below_Batch_Size_Should_Commit_When_Batch_Completes()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 2;
        await using var consumer = new DekafConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));
        await consumer.AckAsync(CreateMessage(0, partition: 1));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets =>
                offsets.Count() == 2
                && offsets.Any(o => o.Partition == 0 && o.Offset == 42)
                && offsets.Any(o => o.Partition == 1 && o.Offset == 1)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Acking_Several_Offsets_Of_A_Partition_Should_Commit_Only_The_Highest()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 3;
        await using var consumer = new DekafConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));
        await consumer.AckAsync(CreateMessage(42));
        await consumer.AckAsync(CreateMessage(43));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset == 44),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Acking_Behind_A_Committed_Offset_Should_Not_Commit_Backwards()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 1;
        await using var consumer = new DekafConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(50));
        await UntilAsync(() => CommitWasCalled(kafkaConsumer));

        // A redelivery settled after the higher offset was committed must not move the group's
        // committed offset back, which would replay everything in between.
        await consumer.AckAsync(CreateMessage(30));

        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Any<IEnumerable<TopicPartitionOffset>>(),
            Arg.Any<CancellationToken>());
        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset == 51),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Sweep_Interval_Elapses_Should_Commit_Uncommitted_Offsets()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 100;
        subscription.SweepUncommittedOffsetsInterval = TimeSpan.FromMilliseconds(50);
        await using var consumer = new DekafConsumer(kafkaConsumer, subscription);

        await consumer.AckAsync(CreateMessage(41));

        await UntilAsync(() => CommitWasCalled(kafkaConsumer));
        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset == 42),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Disposing_Should_Commit_Outstanding_Offsets_And_Close()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        var subscription = CreateSubscription();
        subscription.CommitBatchSize = 100;

        var consumer = new DekafConsumer(kafkaConsumer, subscription);
        await consumer.AckAsync(CreateMessage(41));

        await consumer.DisposeAsync();

        await kafkaConsumer.Received(1).CommitAsync(
            Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset == 42),
            Arg.Any<CancellationToken>());
        await kafkaConsumer.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        await kafkaConsumer.Received(1).DisposeAsync();
    }

    [Test]
    public async Task When_Deferring_Should_Seek_Back_To_The_Message_Offset()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        await using var consumer = new DekafConsumer(kafkaConsumer, CreateSubscription());

        await consumer.DeferAsync(CreateMessage(41), TimeSpan.Zero);

        kafkaConsumer.Positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(tpo =>
            tpo.Topic == TopicName && tpo.Partition == 0 && tpo.Offset == 41));
    }

    [Test]
    public async Task When_Deferring_With_Delay_Should_Pause_And_Resume_The_Partition()
    {
        var kafkaConsumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        await using var consumer = new DekafConsumer(kafkaConsumer, CreateSubscription());

        await consumer.DeferAsync(CreateMessage(41), TimeSpan.FromMilliseconds(50));

        kafkaConsumer.Partitions.Received(1).Pause(
            Arg.Is<TopicPartition[]>(partitions =>
                partitions.Single() == new TopicPartition(TopicName, 0)));

        await UntilAsync(() => kafkaConsumer.Partitions.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IConsumerPartitions.Resume)));
        kafkaConsumer.Partitions.Received(1).Resume(
            Arg.Is<TopicPartition[]>(partitions =>
                partitions.Single() == new TopicPartition(TopicName, 0)));
    }
}
