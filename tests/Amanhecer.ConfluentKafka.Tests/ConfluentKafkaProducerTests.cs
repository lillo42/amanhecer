using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;
using NSubstitute;

namespace Amanhecer.ConfluentKafka.Tests;

/// <summary>
/// Broker-free tests for <see cref="ConfluentKafkaProducer"/>: the mapping from
/// <see cref="Message"/> to the Kafka record, and the fire-and-forget vs
/// wait-for-confirmation publish paths.
/// </summary>
public class ConfluentKafkaProducerTests
{
    private static readonly Guid s_guidHeader = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    private static ConfluentKafkaPublication CreatePublication()
    {
        return new ConfluentKafkaPublication
        {
            RoutingKey = "tests",
            Topic = "tests.topic"
        };
    }

    private static string? GetHeader(Headers headers, string key)
    {
        return headers.TryGetLastBytes(key, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
    }

    private static bool MessageIsMapped(Message<string?, byte[]> m, Message message)
    {
        return m.Key == "partition-key"
            && m.Value.AsSpan().SequenceEqual(message.Payload.Span)
            && GetHeader(m.Headers, "string-header") == "value"
            && GetHeader(m.Headers, "ce_id") == message.Id
            && GetHeader(m.Headers, "ce_type") == "tests.message"
            && GetHeader(m.Headers, "ce_subject") == "subject"
            && GetHeader(m.Headers, "ce_correlationid") == message.CorrelationId;
    }

    private static bool TypedHeadersAreEncoded(Message<string?, byte[]> m)
    {
        return m.Headers.TryGetLastBytes("int-header", out var intBytes)
            && intBytes.AsSpan().SequenceEqual(BitConverter.GetBytes(42))
            && m.Headers.TryGetLastBytes("bool-header", out var boolBytes)
            && boolBytes.AsSpan().SequenceEqual(BitConverter.GetBytes(true))
            && m.Headers.TryGetLastBytes("bytes-header", out var bytesHeader)
            && bytesHeader.AsSpan().SequenceEqual(new byte[] { 1, 2, 3 })
            && m.Headers.TryGetLastBytes("guid-header", out var guidBytes)
            && guidBytes.AsSpan().SequenceEqual(s_guidHeader.ToByteArray());
    }

    private static bool HasCloudEventHeaders(Message<string?, byte[]> m, Message message)
    {
        return GetHeader(m.Headers, "ce_id") == message.Id
            && GetHeader(m.Headers, "ce_type") == message.Type
            && GetHeader(m.Headers, "ce_correlationid") == message.CorrelationId;
    }

    [Test]
    public async Task When_Producing_Should_Map_Message_To_Kafka_Message()
    {
        var kafkaProducer = Substitute.For<IProducer<string?, byte[]>>();
        var producer = new ConfluentKafkaProducer(kafkaProducer);
        var publication = CreatePublication();
        var message = new Message
        {
            PartitionKey = "partition-key",
            Type = "tests.message",
            Subject = "subject",
            Payload = Encoding.UTF8.GetBytes("payload"),
            Headers =
            {
                ["string-header"] = "value",
                ["int-header"] = 42,
                ["bytes-header"] = new byte[] { 1, 2, 3 }
            }
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        kafkaProducer.Received(1).Produce(
            publication.Topic,
            Arg.Is<Message<string?, byte[]>>(m => MessageIsMapped(m, message)),
            Arg.Any<Action<DeliveryReport<string?, byte[]>>?>());
    }

    [Test]
    public async Task When_Producing_Should_Encode_Typed_Headers_To_Bytes()
    {
        var kafkaProducer = Substitute.For<IProducer<string?, byte[]>>();
        var producer = new ConfluentKafkaProducer(kafkaProducer);
        var publication = CreatePublication();
        var message = new Message
        {
            Payload = Array.Empty<byte>(),
            Headers =
            {
                ["int-header"] = 42,
                ["bool-header"] = true,
                ["bytes-header"] = new byte[] { 1, 2, 3 },
                ["guid-header"] = s_guidHeader
            }
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        kafkaProducer.Received(1).Produce(
            Arg.Any<string>(),
            Arg.Is<Message<string?, byte[]>>(m => TypedHeadersAreEncoded(m)),
            Arg.Any<Action<DeliveryReport<string?, byte[]>>?>());
    }

    [Test]
    public async Task When_CloudEventType_Is_Not_Binary_Should_Still_Write_CloudEvent_Headers()
    {
        var kafkaProducer = Substitute.For<IProducer<string?, byte[]>>();
        var producer = new ConfluentKafkaProducer(kafkaProducer);
        var publication = CreatePublication();
        publication.CloudEventType = CloudEventType.Json;
        var message = new Message
        {
            Payload = Array.Empty<byte>(),
            Type = "tests.message"
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        kafkaProducer.Received(1).Produce(
            Arg.Any<string>(),
            Arg.Is<Message<string?, byte[]>>(m => HasCloudEventHeaders(m, message)),
            Arg.Any<Action<DeliveryReport<string?, byte[]>>?>());
    }

    [Test]
    public async Task When_WaitForConfirmation_Should_Await_The_Delivery_Report()
    {
        var kafkaProducer = Substitute.For<IProducer<string?, byte[]>>();
        kafkaProducer
            .ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string?, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string?, byte[]>()));
        var producer = new ConfluentKafkaProducer(kafkaProducer);
        var publication = CreatePublication();
        publication.WaitForConfirmation = true;
        var message = new Message { Payload = Array.Empty<byte>() };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await kafkaProducer.Received(1).ProduceAsync(
            publication.Topic,
            Arg.Any<Message<string?, byte[]>>(),
            Arg.Any<CancellationToken>());
        kafkaProducer.DidNotReceive().Produce(
            Arg.Any<string>(),
            Arg.Any<Message<string?, byte[]>>(),
            Arg.Any<Action<DeliveryReport<string?, byte[]>>?>());
    }

    [Test]
    public async Task When_Publication_Is_Not_ConfluentKafkaPublication_Should_Not_Produce()
    {
        var kafkaProducer = Substitute.For<IProducer<string?, byte[]>>();
        var producer = new ConfluentKafkaProducer(kafkaProducer);
        var message = new Message { Payload = Array.Empty<byte>() };

        await producer.ProduceAsync(message, Substitute.For<IPublication>(), new AmanhecerContext());

        kafkaProducer.DidNotReceive().Produce(
            Arg.Any<string>(),
            Arg.Any<Message<string?, byte[]>>(),
            Arg.Any<Action<DeliveryReport<string?, byte[]>>?>());
    }
}
