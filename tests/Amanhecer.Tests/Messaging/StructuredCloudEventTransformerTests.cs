using System;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class StructuredCloudEventTransformerTests
{
    private readonly StructuredCloudEventTransformer _transformer = new();

    [Test]
    public async Task EncodeAsync_With_A_Json_Publication_Should_Wrap_The_Payload_In_The_Envelope()
    {
        var publication = CreatePublication();
        var context = CreateContext(publication);
        var message = CreateMessage();
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.ContentType!.MediaType)
            .IsEqualTo(StructuredCloudEventTransformer.MediaType);

        using var envelope = JsonDocument.Parse(message.Payload);
        var root = envelope.RootElement;
        await Assert.That(root.GetProperty("specversion").GetString()).IsEqualTo("1.0");
        await Assert.That(root.GetProperty("id").GetString()).IsEqualTo(message.Id);
        await Assert.That(root.GetProperty("source").GetString()).IsEqualTo("amanhecer.tests");
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("amanhecer.tests.message");
        await Assert.That(root.GetProperty("datacontenttype").GetString()).IsEqualTo("application/json");
        await Assert.That(root.GetProperty("time").GetString())
            .IsEqualTo(message.Time.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        // A JSON payload is embedded as a raw JSON token, not a string.
        await Assert.That(root.GetProperty("data").ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(root.GetProperty("data").GetProperty("greeting").GetString()).IsEqualTo("hello");
        await Assert.That(root.TryGetProperty("data_base64", out _)).IsFalse();
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task EncodeAsync_With_A_Binary_Publication_Should_Pass_Through()
    {
        var publication = CreatePublication();
        publication.CloudEventType = CloudEventType.Binary;
        var context = CreateContext(publication);
        var message = CreateMessage();
        var payload = message.Payload;
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload.ToArray());
        await Assert.That(message.ContentType!.MediaType).IsEqualTo("application/json");
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task EncodeAsync_Without_A_Publication_Should_Pass_Through()
    {
        var context = new AmanhecerContext();
        var message = CreateMessage();
        var payload = message.Payload;
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload.ToArray());
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task EncodeAsync_With_A_Non_Json_Payload_Should_Embed_Data_Base64()
    {
        var publication = CreatePublication();
        var context = CreateContext(publication);
        var message = CreateMessage();
        message.ContentType = new ContentType("text/plain");
        message.Payload = "plain text"u8.ToArray();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        using var envelope = JsonDocument.Parse(message.Payload);
        await Assert.That(envelope.RootElement.TryGetProperty("data", out _)).IsFalse();
        await Assert.That(envelope.RootElement.GetProperty("data_base64").GetString())
            .IsEqualTo(Convert.ToBase64String("plain text"u8.ToArray()));
    }

    [Test]
    public async Task EncodeAsync_With_An_Invalid_Json_Payload_Should_Embed_Data_Base64()
    {
        var publication = CreatePublication();
        var context = CreateContext(publication);
        var message = CreateMessage();
        message.Payload = "{not json"u8.ToArray();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        using var envelope = JsonDocument.Parse(message.Payload);
        await Assert.That(envelope.RootElement.TryGetProperty("data", out _)).IsFalse();
        await Assert.That(envelope.RootElement.GetProperty("data_base64").GetString())
            .IsEqualTo(Convert.ToBase64String("{not json"u8.ToArray()));
    }

    [Test]
    public async Task EncodeAsync_Should_Embed_Extension_And_Tracing_Attributes()
    {
        var publication = CreatePublication();
        publication.AdditionalCloudEvents["tenant"] = "acme";
        publication.AdditionalCloudEvents["retries"] = 3;
        var context = CreateContext(publication);
        var message = CreateMessage();
        message.TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        message.TraceState = TraceState.FromString("vendor=value");
        message.Baggage = Baggage.FromString("user=alice");

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        using var envelope = JsonDocument.Parse(message.Payload);
        var root = envelope.RootElement;
        await Assert.That(root.GetProperty("tenant").GetString()).IsEqualTo("acme");
        await Assert.That(root.GetProperty("retries").GetInt64()).IsEqualTo(3);
        await Assert.That(root.GetProperty("traceparent").GetString()).IsEqualTo(message.TraceParent);
        await Assert.That(root.GetProperty("tracestate").GetString()).IsEqualTo("vendor=value");
        await Assert.That(root.GetProperty("baggage").GetString()).Contains("user=alice");
    }

    [Test]
    public async Task EncodeAsync_With_An_Extension_Attribute_Matching_A_Standard_Attribute_Should_Not_Overwrite_It()
    {
        var publication = CreatePublication();
        publication.AdditionalCloudEvents["type"] = "overwritten";
        var context = CreateContext(publication);
        var message = CreateMessage();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        using var envelope = JsonDocument.Parse(message.Payload);
        await Assert.That(envelope.RootElement.GetProperty("type").GetString())
            .IsEqualTo("amanhecer.tests.message");
    }

    [Test]
    public async Task EncodeAsync_Should_Fill_Missing_Attributes_From_The_Publication_Defaults()
    {
        var publication = CreatePublication();
        publication.DefaultType = "amanhecer.tests.default";
        var context = CreateContext(publication);
        var message = new Message
        {
            Id = "",
            Payload = """{"greeting":"hello"}"""u8.ToArray()
        };

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        using var envelope = JsonDocument.Parse(message.Payload);
        var root = envelope.RootElement;
        await Assert.That(root.GetProperty("id").GetString()).IsEqualTo(context.RequestId);
        await Assert.That(root.GetProperty("source").GetString())
            .IsEqualTo(publication.DefaultSource.ToString());
        await Assert.That(root.GetProperty("specversion").GetString())
            .IsEqualTo(publication.DefaultSpecVersion);
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("amanhecer.tests.default");
    }

    [Test]
    public async Task DecodeAsync_With_A_Structured_Envelope_Should_Parse_The_Message()
    {
        var time = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var message = new Message
        {
            ContentType = new ContentType("application/cloudevents+json; charset=utf-8"),
            Payload = Encoding.UTF8.GetBytes($$"""
                {
                    "specversion": "1.0",
                    "id": "tests.structured",
                    "source": "amanhecer.tests",
                    "type": "amanhecer.tests.structured",
                    "time": "{{time:O}}",
                    "datacontenttype": "application/json",
                    "dataschema": "https://tests.amanhecer/schema",
                    "subject": "tests.subject",
                    "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                    "tracestate": "vendor=value",
                    "baggage": "user=alice",
                    "tenant": "acme",
                    "data": {"greeting":"hello"}
                }
                """)
        };
        var context = CreateSubscriptionContext();
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.DecodeAsync(message, context, next);

        await Assert.That(message.Id).IsEqualTo("tests.structured");
        await Assert.That(message.Source!.ToString()).IsEqualTo("amanhecer.tests");
        await Assert.That(message.SpecVersion).IsEqualTo("1.0");
        await Assert.That(message.Type).IsEqualTo("amanhecer.tests.structured");
        await Assert.That(message.Time).IsEqualTo(time);
        await Assert.That(message.Subject).IsEqualTo("tests.subject");
        await Assert.That(message.DataSchema!.ToString()).IsEqualTo("https://tests.amanhecer/schema");
        await Assert.That(message.ContentType!.MediaType).IsEqualTo("application/json");
        await Assert.That(message.TraceParent)
            .IsEqualTo("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        await Assert.That(message.TraceState!.ToString()).IsEqualTo("vendor=value");
        await Assert.That(message.Baggage!.ToString()).IsEqualTo("user=alice");
        await Assert.That(Encoding.UTF8.GetString(message.Payload.Span)).IsEqualTo("""{"greeting":"hello"}""");

        // Unknown top-level members are CloudEvents extension attributes.
        await Assert.That(message.Headers["cloudEvents:tenant"]).IsEqualTo("acme");
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_Data_Base64_Should_Decode_The_Payload()
    {
        var payload = "plain text"u8.ToArray();
        var message = new Message
        {
            ContentType = new ContentType(StructuredCloudEventTransformer.MediaType),
            Payload = Encoding.UTF8.GetBytes($$"""
                {
                    "specversion": "1.0",
                    "id": "tests.structured",
                    "source": "amanhecer.tests",
                    "type": "amanhecer.tests.structured",
                    "datacontenttype": "text/plain",
                    "data_base64": "{{Convert.ToBase64String(payload)}}"
                }
                """)
        };

        await _transformer.DecodeAsync(message,
            CreateSubscriptionContext(),
            Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.Payload.ToArray()).IsEquivalentTo(payload);
        await Assert.That(message.ContentType!.MediaType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task DecodeAsync_Without_DataContentType_Should_Clear_The_Content_Type()
    {
        var message = new Message
        {
            ContentType = new ContentType(StructuredCloudEventTransformer.MediaType),
            Payload = """
                      {
                          "specversion": "1.0",
                          "id": "tests.structured",
                          "source": "amanhecer.tests",
                          "type": "amanhecer.tests.structured",
                          "data": {"greeting":"hello"}
                      }
                      """u8.ToArray()
        };

        await _transformer.DecodeAsync(message,
            CreateSubscriptionContext(),
            Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.ContentType).IsNotNull();
    }

    [Test]
    public async Task DecodeAsync_With_Another_Content_Type_Should_Pass_Through()
    {
        var message = new Message
        {
            ContentType = new ContentType("application/json"),
            Payload = """{"greeting":"hello"}"""u8.ToArray()
        };
        var context = CreateSubscriptionContext(CloudEventType.Binary);
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.DecodeAsync(message, context, next);

        await Assert.That(message.ContentType!.MediaType).IsEqualTo("application/json");
        await Assert.That(Encoding.UTF8.GetString(message.Payload.Span)).IsEqualTo("""{"greeting":"hello"}""");
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_A_Non_Object_Envelope_Should_Throw()
    {
        var message = new Message
        {
            ContentType = new ContentType(StructuredCloudEventTransformer.MediaType),
            Payload = "[1, 2, 3]"u8.ToArray()
        };

        await Assert.That(async () => await _transformer.DecodeAsync(message,
                CreateSubscriptionContext(),
                Substitute.For<Func<Message, AmanhecerContext, ValueTask>>()))
            .ThrowsExactly<FormatException>();
    }

    [Test]
    public async Task Encode_Then_Decode_Should_Round_Trip_The_Message()
    {
        var publication = CreatePublication();
        publication.AdditionalCloudEvents["tenant"] = "acme";
        var message = CreateMessage();
        var originalPayload = message.Payload.ToArray();

        await _transformer.EncodeAsync(message,
            CreateContext(publication),
            Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        var decoded = new Message
        {
            ContentType = message.ContentType,
            Payload = message.Payload
        };
        await _transformer.DecodeAsync(decoded,
            CreateSubscriptionContext(),
            Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(decoded.Id).IsEqualTo(message.Id);
        await Assert.That(decoded.Source!.ToString()).IsEqualTo("amanhecer.tests");
        await Assert.That(decoded.SpecVersion).IsEqualTo("1.0");
        await Assert.That(decoded.Type).IsEqualTo("amanhecer.tests.message");
        await Assert.That(decoded.Time).IsEqualTo(message.Time);
        await Assert.That(decoded.ContentType!.MediaType).IsEqualTo("application/json");
        await Assert.That(decoded.Payload.ToArray()).IsEquivalentTo(originalPayload);
        await Assert.That(decoded.Headers["cloudEvents:tenant"]).IsEqualTo("acme");
    }

    private static TestPublication CreatePublication()
    {
        return new TestPublication
        {
            RoutingKey = "tests",
            CloudEventType = CloudEventType.Json,
            DefaultType = "amanhecer.tests.default"
        };
    }

    private static AmanhecerContext CreateContext(IPublication publication)
    {
        var context = new AmanhecerContext();
        context.SetMetadata(publication, MetadataName.Publication);
        return context;
    }

    private static AmanhecerContext CreateSubscriptionContext(CloudEventType cloudEventType = CloudEventType.Json)
    {
        var subscription = new TestSubscription("tests") { CloudEventType = cloudEventType };
        var context = new AmanhecerContext();
        context.SetMetadata(subscription, MetadataName.Subscription);
        return context;
    }

    private static Message CreateMessage()
    {
        return new Message
        {
            ContentType = new ContentType("application/json"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = """{"greeting":"hello"}"""u8.ToArray()
        };
    }
}
