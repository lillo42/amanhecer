using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Dekaf.Producer;
using NSubstitute;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Broker-free tests for <see cref="DeKafProducer"/>: the mapping from <see cref="Message"/>
/// to the Kafka record, and the fire-and-forget vs wait-for-confirmation publish paths.
/// </summary>
public class DekafProducerTests
{
    private static readonly Guid s_guidHeader = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    private static DekafPublication CreatePublication()
    {
        return new DekafPublication
        {
            RoutingKey = "tests",
            Topic = "tests.topic"
        };
    }

    private static string? GetHeader(global::Dekaf.Serialization.Headers? headers, string key)
    {
        return headers?.GetFirst(key)?.GetValueAsString();
    }

    private static bool MessageIsMapped(ProducerMessage<string, byte[]> m, Message message)
    {
        return m.Topic == "tests.topic"
            && m.Key == "partition-key"
            && m.Value.AsSpan().SequenceEqual(message.Payload.Span)
            && GetHeader(m.Headers, "string-header") == "value"
            && GetHeader(m.Headers, "ce_id") == message.Id
            && GetHeader(m.Headers, "ce_type") == "tests.message"
            && GetHeader(m.Headers, "ce_subject") == "subject"
            && GetHeader(m.Headers, "ce_correlationid") == message.CorrelationId;
    }

    private static bool HeaderBytesEqual(global::Dekaf.Serialization.Headers headers, string key, byte[] expected)
    {
        var header = headers.GetFirst(key);
        return header != null && header.Value.Value.Span.SequenceEqual(expected);
    }

    private static bool TypedHeadersAreEncoded(ProducerMessage<string, byte[]> m)
    {
        return m.Headers != null
            && HeaderBytesEqual(m.Headers, "int-header", BitConverter.GetBytes(42))
            && HeaderBytesEqual(m.Headers, "bool-header", BitConverter.GetBytes(true))
            && HeaderBytesEqual(m.Headers, "bytes-header", new byte[] { 1, 2, 3 })
            && HeaderBytesEqual(m.Headers, "guid-header", s_guidHeader.ToByteArray());
    }

    private static bool HasCloudEventHeaders(ProducerMessage<string, byte[]> m, Message message)
    {
        return GetHeader(m.Headers, "ce_id") == message.Id
            && GetHeader(m.Headers, "ce_type") == message.Type
            && GetHeader(m.Headers, "ce_correlationid") == message.CorrelationId;
    }

    [Test]
    public async Task When_Producing_Should_Map_Message_To_Kafka_Message()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        var producer = new DeKafProducer(kafkaProducer);
        var publication = CreatePublication();
        var message = new Message
        {
            PartitionKey = "partition-key",
            Type = "tests.message",
            Subject = "subject",
            Payload = "payload"u8.ToArray(),
            Headers =
            {
                ["string-header"] = "value",
                ["int-header"] = 42,
                ["bytes-header"] = new byte[] { 1, 2, 3 }
            }
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await kafkaProducer.Received(1).FireAsync(
            Arg.Is<ProducerMessage<string, byte[]>>(m => MessageIsMapped(m, message)));
    }

    [Test]
    public async Task When_Producing_Should_Encode_Typed_Headers_To_Bytes()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        var producer = new DeKafProducer(kafkaProducer);
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

        await kafkaProducer.Received(1).FireAsync(
            Arg.Is<ProducerMessage<string, byte[]>>(m => TypedHeadersAreEncoded(m)));
    }

    [Test]
    public async Task When_CloudEventType_Is_Not_Binary_Should_Still_Write_CloudEvent_Headers()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        var producer = new DeKafProducer(kafkaProducer);
        var publication = CreatePublication();
        publication.CloudEventType = CloudEventType.Json;
        var message = new Message
        {
            Payload = Array.Empty<byte>(),
            Type = "tests.message"
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await kafkaProducer.Received(1).FireAsync(
            Arg.Is<ProducerMessage<string, byte[]>>(m => HasCloudEventHeaders(m, message)));
    }

    [Test]
    public async Task When_Message_Has_ContentEncoding_Should_Write_ContentEncoding_Header()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        var producer = new DeKafProducer(kafkaProducer);
        var publication = CreatePublication();
        var message = new Message
        {
            Payload = Array.Empty<byte>(),
            ContentEncoding = "br"
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await kafkaProducer.Received(1).FireAsync(
            Arg.Is<ProducerMessage<string, byte[]>>(m => GetHeader(m.Headers, "Content-Encoding") == "br"));
    }

    [Test]
    public async Task When_WaitForConfirmation_Should_Await_The_Delivery_Result()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        kafkaProducer
            .ProduceAsync(Arg.Any<ProducerMessage<string, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(new RecordMetadata
            {
                Topic = "tests.topic",
                Partition = 0,
                Offset = 0,
                Timestamp = DateTimeOffset.UtcNow
            }));
        var producer = new DeKafProducer(kafkaProducer);
        var publication = CreatePublication();
        publication.WaitForConfirmation = true;
        var message = new Message { Payload = Array.Empty<byte>() };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await kafkaProducer.Received(1).ProduceAsync(
            Arg.Any<ProducerMessage<string, byte[]>>(),
            Arg.Any<CancellationToken>());
        await kafkaProducer.DidNotReceive().FireAsync(Arg.Any<ProducerMessage<string, byte[]>>());
    }

    [Test]
    public async Task When_Publication_Is_Not_DekafPublication_Should_Not_Produce()
    {
        var kafkaProducer = Substitute.For<IKafkaProducer<string, byte[]>>();
        var producer = new DeKafProducer(kafkaProducer);
        var message = new Message { Payload = Array.Empty<byte>() };

        await producer.ProduceAsync(message, Substitute.For<IPublication>(), new AmanhecerContext());

        await kafkaProducer.DidNotReceive().FireAsync(Arg.Any<ProducerMessage<string, byte[]>>());
    }
}
