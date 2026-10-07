using System;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-independent tests for <see cref="RabbitMqStreamProducer"/>: the argument validation it
/// performs and the AMQP message it builds from an Amanhecer <see cref="Message"/>.
/// </summary>
public class RabbitMqStreamProducerTests
{
    [Test]
    public async Task When_Producing_Through_A_Non_Stream_Publication_Should_Throw()
    {
        var producer = new RabbitMqStreamProducer(null!);

        await Assert
            .That(async () => await producer.ProduceAsync(
                new Message(),
                new TestPublication { RoutingKey = "tests" },
                new AmanhecerContext()))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Building_The_Amqp_Message_Should_Map_The_Standard_Properties()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };

        var rmqMessage = ToRabbitMqMessage(message, publication, new AmanhecerContext());

        var properties = rmqMessage.Properties;
        await Assert.That(properties.CorrelationId?.ToString()).IsEqualTo(message.CorrelationId);
        await Assert.That(properties.MessageId?.ToString()).IsEqualTo(message.Id);
        await Assert.That(properties.ContentEncoding).IsEqualTo(message.ContentEncoding);
        await Assert.That(properties.ContentType).IsEqualTo(message.ContentType.ToString());
        await Assert.That(properties.Subject).IsEqualTo(message.Subject);
        await Assert.That(properties.ReplyTo).IsEqualTo(message.ReplyTo);
        await Assert.That(properties.GroupId).IsEqualTo(message.PartitionKey);
    }

    [Test]
    public async Task When_The_Publication_Has_A_User_Id_Should_Encode_It_Onto_The_Message()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream")
        {
            RoutingKey = "tests",
            UserId = "tests.user",
            Encoding = Encoding.UTF8
        };

        var rmqMessage = ToRabbitMqMessage(message, publication, new AmanhecerContext());

        await Assert
            .That(Encoding.UTF8.GetString(rmqMessage.Properties.UserId))
            .IsEqualTo("tests.user");
    }

    [Test]
    public async Task When_The_Context_Sets_An_Expiration_Should_Map_The_Absolute_Expiry_Time()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };
        var expireAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var context = new AmanhecerContext();
        context.Metadata[Metadata.Expiration] = expireAt;

        var rmqMessage = ToRabbitMqMessage(message, publication, context);

        await Assert.That(rmqMessage.Properties.AbsoluteExpiryTime).IsEqualTo(expireAt);
    }

    [Test]
    public async Task When_The_Message_Metadata_Sets_An_Expiration_Should_Map_The_Absolute_Expiry_Time()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };
        var expireAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        message.Metadata[Metadata.Expiration] = expireAt;

        var rmqMessage = ToRabbitMqMessage(message, publication, new AmanhecerContext());

        await Assert.That(rmqMessage.Properties.AbsoluteExpiryTime).IsEqualTo(expireAt);
    }

    [Test]
    public async Task When_Both_Message_And_Context_Set_An_Expiration_Should_Prefer_The_Message()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };
        var messageExpireAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        message.Metadata[Metadata.Expiration] = messageExpireAt;
        var context = new AmanhecerContext();
        context.Metadata[Metadata.Expiration] = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var rmqMessage = ToRabbitMqMessage(message, publication, context);

        await Assert.That(rmqMessage.Properties.AbsoluteExpiryTime).IsEqualTo(messageExpireAt);
    }

    [Test]
    public async Task When_The_Message_Carries_Amqp_Metadata_Should_Map_It_Onto_The_Message()
    {
        var message = CreateMessage();
        message.Metadata[Metadata.GroupSequence] = (uint)7;
        message.Metadata[Metadata.To] = "to.target";
        message.Metadata[Metadata.ReplyToGroupId] = "reply.group";
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };

        var rmqMessage = ToRabbitMqMessage(message, publication, new AmanhecerContext());

        await Assert.That(rmqMessage.Properties.GroupSequence).IsEqualTo((uint)7);
        await Assert.That(rmqMessage.Properties.To).IsEqualTo("to.target");
        await Assert.That(rmqMessage.Properties.ReplyToGroupId).IsEqualTo("reply.group");
    }

    [Test]
    public async Task When_The_Message_Has_Headers_Should_Copy_Them_To_Application_Properties()
    {
        var message = CreateMessage();
        message.Headers["tenant"] = "acme";
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };

        var rmqMessage = ToRabbitMqMessage(message, publication, new AmanhecerContext());

        await Assert.That(rmqMessage.ApplicationProperties["tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task When_Setting_The_CloudEvents_Headers_Should_Populate_The_Standard_Attributes()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };

        SetCloudEventHeaders(message, publication);

        await Assert.That(message.Headers["cloudEvents:id"]).IsEqualTo(message.Id);
        await Assert.That(message.Headers["cloudEvents:source"]).IsEqualTo(message.Source!.ToString());
        await Assert.That(message.Headers["cloudEvents:specversion"]).IsEqualTo(message.SpecVersion);
        await Assert.That(message.Headers["cloudEvents:type"]).IsEqualTo(message.Type);
        await Assert.That(message.Headers["cloudEvents:datacontenttype"]).IsEqualTo(message.ContentType.ToString());
    }

    [Test]
    public async Task When_Setting_The_CloudEvents_Headers_With_Additional_Attributes_Should_Prefix_Them()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };
        publication.AdditionalCloudEvents["tenant"] = "acme";

        SetCloudEventHeaders(message, publication);

        await Assert.That(message.Headers["cloudEvents:tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task When_An_Additional_CloudEvent_Matches_A_Standard_Attribute_Should_Not_Overwrite_It()
    {
        var message = CreateMessage();
        var publication = new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" };
        publication.AdditionalCloudEvents["type"] = "overwritten";

        SetCloudEventHeaders(message, publication);

        await Assert.That(message.Headers["cloudEvents:type"]).IsEqualTo(message.Type);
    }

    private static RabbitMQ.Stream.Client.Message ToRabbitMqMessage(
        Message message,
        RabbitMqStreamPublication publication,
        AmanhecerContext context)
    {
        var method = typeof(RabbitMqStreamProducer).GetMethod(
            "ToRabbitMqMessage",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        return (RabbitMQ.Stream.Client.Message)method.Invoke(null, [message, publication, context])!;
    }

    private static void SetCloudEventHeaders(Message message, IPublication publication)
    {
        var method = typeof(RabbitMqStreamProducer).GetMethod(
            "SetCloudEventHeaders",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        method.Invoke(null, [message, publication]);
    }

    private static Message CreateMessage()
    {
        return new Message
        {
            Id = Uuid.NewGuid().ToString(),
            CorrelationId = Uuid.NewGuid().ToString(),
            ContentEncoding = "utf-8",
            ContentType = new ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Subject = "tests.subject",
            ReplyTo = "tests.reply",
            PartitionKey = "tests.partition",
            Payload = Encoding.UTF8.GetBytes("payload")
        };
    }

    private sealed class TestPublication : Publication;
}
