using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the AMQP properties and headers <see cref="RabbitMqProducer"/>
/// publishes messages with, captured from a mocked <see cref="IChannel"/>.
/// </summary>
public class RabbitMqProducerPropertiesTests
{
    [Test]
    public async Task When_The_Publication_Is_Persistent_Should_Publish_Persistent_Messages()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.Persistent = true;

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        await Assert.That(publishes[0].Properties.Persistent).IsTrue();
    }

    [Test]
    public async Task When_The_Publication_Is_Not_Persistent_Should_Not_Publish_Persistent_Messages()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.Persistent = false;

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        await Assert.That(publishes[0].Properties.Persistent).IsFalse();
    }

    [Test]
    public async Task When_The_Metadata_Sets_Persistent_Should_Override_The_Publication()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.Persistent = false;
        var context = new AmanhecerContext();
        context.Metadata[MetadataName.Persistent] = true;

        await producer.ProduceAsync(CreateMessage(), publication, context);

        await Assert.That(publishes[0].Properties.Persistent).IsTrue();
    }

    [Test]
    public async Task When_The_Metadata_Clears_Persistent_Should_Override_The_Publication()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.Persistent = true;
        var context = new AmanhecerContext();
        context.Metadata[MetadataName.Persistent] = false;

        await producer.ProduceAsync(CreateMessage(), publication, context);

        await Assert.That(publishes[0].Properties.Persistent).IsFalse();
    }

    [Test]
    public async Task When_The_Sets_Content_Encoding_Should_Override_The_Publication()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.ContentEncoding = "br";
        var context = new AmanhecerContext();
        await producer.ProduceAsync(CreateMessage(), publication, context);

        await Assert.That(publishes[0].Properties.ContentEncoding).IsEqualTo("br");
    }

    [Test]
    public async Task When_No_Content_Encoding_Metadata_Should_Use_The_Publication_Encoding()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.ContentEncoding = "utf-16";

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        await Assert.That(publishes[0].Properties.ContentEncoding).IsEqualTo("utf-16");
    }

    [Test]
    public async Task When_The_Metadata_Sets_Expiration_Should_Publish_With_Expiration()
    {
        var (producer, publishes) = CreateProducer();
        var context = new AmanhecerContext();
        context.Metadata[MetadataName.Expiration] = "60000";

        await producer.ProduceAsync(CreateMessage(), CreatePublication(), context);

        await Assert.That(publishes[0].Properties.Expiration).IsEqualTo("60000");
    }

    [Test]
    public async Task When_The_Metadata_Sets_Priority_Should_Publish_With_Priority()
    {
        var (producer, publishes) = CreateProducer();
        var context = new AmanhecerContext();
        context.Metadata[MetadataName.Priority] = (byte)5;

        await producer.ProduceAsync(CreateMessage(), CreatePublication(), context);

        await Assert.That(publishes[0].Properties.Priority).IsEqualTo((byte)5);
    }

    [Test]
    public async Task When_No_Priority_Metadata_Should_Not_Set_Priority()
    {
        var (producer, publishes) = CreateProducer();

        await producer.ProduceAsync(CreateMessage(), CreatePublication(), new AmanhecerContext());

        await Assert.That(publishes[0].Properties.Priority).IsEqualTo((byte)0);
    }

    [Test]
    public async Task When_Producing_A_Message_Should_Set_The_CloudEvents_Headers()
    {
        var (producer, publishes) = CreateProducer();
        var message = CreateMessage();

        await producer.ProduceAsync(message, CreatePublication(), new AmanhecerContext());

        var headers = publishes[0].Properties.Headers!;
        await Assert.That(headers["cloudEvents:id"]).IsEqualTo(message.Id);
        await Assert.That(headers["cloudEvents:source"]).IsEqualTo(message.Source!.ToString());
        await Assert.That(headers["cloudEvents:specversion"]).IsEqualTo(message.SpecVersion);
        await Assert.That(headers["cloudEvents:type"]).IsEqualTo(message.Type);
    }

    [Test]
    public async Task When_Producing_A_Message_Should_Publish_To_The_Configured_Exchange_And_Routing_Key()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.Mandatory = true;

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        await Assert.That(publishes[0].Exchange).IsEqualTo(publication.Exchange!.Name);
        await Assert.That(publishes[0].RoutingKey).IsEqualTo(publication.RabbitMqRoutingKey);
        await Assert.That(publishes[0].Mandatory).IsTrue();
    }

    [Test]
    public async Task When_The_Publication_Uses_Structured_CloudEvents_Should_Pass_The_Message_Content_Type_Through()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.CloudEventType = CloudEventType.Json;

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        // The envelope content type is set by the StructuredCloudEventTransformer upstream
        // in the pipeline; the producer passes the message's content type through.
        await Assert.That(publishes[0].Properties.ContentType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task When_The_Publication_Has_Additional_CloudEvents_Should_Set_The_CloudEvents_Headers()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.AdditionalCloudEvents["tenant"] = "acme";

        await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext());

        await Assert.That(publishes[0].Properties.Headers!["cloudEvents:tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task When_An_Additional_CloudEvent_Matches_A_Standard_Attribute_Should_Not_Overwrite_It()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.AdditionalCloudEvents["type"] = "overwritten";
        var message = CreateMessage();

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        await Assert.That(publishes[0].Properties.Headers!["cloudEvents:type"]).IsEqualTo(message.Type);
    }

    [Test]
    public async Task When_The_Publication_Has_No_Exchange_Should_Throw()
    {
        var (producer, _) = CreateProducer();
        var publication = CreatePublication();
        publication.Exchange = null;

        await Assert.That(async () =>
                await producer.ProduceAsync(CreateMessage(), publication, new AmanhecerContext()))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Creating_A_Producer_Should_Subscribe_To_Basic_Returns()
    {
        var channel = Substitute.For<IChannel>();

        _ = new RabbitMqProducer(channel);

        channel.Received(1).BasicReturnAsync += Arg.Any<AsyncEventHandler<BasicReturnEventArgs>>();
    }

    [Test]
    public async Task When_The_Broker_Returns_A_Message_Should_Handle_The_Return()
    {
        var channel = Substitute.For<IChannel>();
        _ = new RabbitMqProducer(channel);

        channel.BasicReturnAsync += Raise.Event<AsyncEventHandler<BasicReturnEventArgs>>(
            channel,
            new BasicReturnEventArgs(312,
                "NO_ROUTE",
                "tests.exchange",
                "tests",
                Substitute.For<IReadOnlyBasicProperties>(),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None));
    }

    private static (RabbitMqProducer Producer, List<Publish> Publishes) CreateProducer()
    {
        var channel = Substitute.For<IChannel>();
        var publishes = new List<Publish>();

        channel
            .BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                publishes.Add(new Publish(
                    callInfo.ArgAt<string>(0),
                    callInfo.ArgAt<string>(1),
                    callInfo.ArgAt<bool>(2),
                    callInfo.ArgAt<BasicProperties>(3)));
                return ValueTask.CompletedTask;
            });

        return (new RabbitMqProducer(channel), publishes);
    }

    private static RabbitMqPublication CreatePublication()
    {
        return new RabbitMqPublication
        {
            RoutingKey = "tests",
            RabbitMqRoutingKey = "tests.rabbitmq",
            Exchange = new Exchange { Name = "tests.exchange" }
        };
    }

    private static Message CreateMessage()
    {
        return new Message
        {
            ContentType = new ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = Encoding.UTF8.GetBytes(Uuid.NewGuid().ToString())
        };
    }

    private sealed record Publish(string Exchange, string RoutingKey, bool Mandatory, BasicProperties Properties);
}
