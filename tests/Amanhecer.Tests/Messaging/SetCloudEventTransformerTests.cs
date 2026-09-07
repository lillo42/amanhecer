using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class SetCloudEventTransformerTests
{
    private readonly SetCloudEventTransformer _transformer =
        new(NullLogger<SetCloudEventTransformer>.Instance);

    [Test]
    public async Task EncodeAsync_With_CloudEventAttribute_Should_ApplyAttributesToEmptyMessage()
    {
        var attribute = new CloudEventAttribute(0)
        {
            ContentType = "application/json",
            DataSchema = "https://example.com/schema",
            ReplyTo = "reply.queue",
            Source = "https://example.com/source",
            SpecVersion = "1.0",
            Subject = "subject",
            Type = "com.example.event"
        };
        var context = new AmanhecerContext();
        context.SetMetadata(attribute);
        var message = new Message();
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.ContentType!.MediaType).IsEqualTo("application/json");
        await Assert.That(message.DataSchema!.ToString()).IsEqualTo("https://example.com/schema");
        await Assert.That(message.ReplyTo).IsEqualTo("reply.queue");
        await Assert.That(message.Source!.ToString()).IsEqualTo("https://example.com/source");
        await Assert.That(message.SpecVersion).IsEqualTo("1.0");
        await Assert.That(message.Subject).IsEqualTo("subject");
        await Assert.That(message.Type).IsEqualTo("com.example.event");
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task EncodeAsync_With_CloudEventAttribute_Should_NotOverrideValuesAlreadySet()
    {
        var attribute = new CloudEventAttribute(0)
        {
            ContentType = "application/json",
            Subject = "attribute-subject",
            SpecVersion = "0.3",
            Type = "com.example.event"
        };
        var context = new AmanhecerContext();
        context.SetMetadata(attribute);
        var message = new Message
        {
            ContentType = new ContentType("text/plain"),
            Subject = "message-subject",
            SpecVersion = "1.0",
            Type = "com.example.existing"
        };

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.ContentType.MediaType).IsEqualTo("text/plain");
        await Assert.That(message.Subject).IsEqualTo("message-subject");
        await Assert.That(message.SpecVersion).IsEqualTo("1.0");
        await Assert.That(message.Type).IsEqualTo("com.example.existing");
    }

    [Test]
    public async Task EncodeAsync_With_Publication_Should_ApplyPublicationDefaults()
    {
        var publication = new TestPublication
        {
            RoutingKey = "orders",
            DefaultType = "com.example.order",
            DefaultSubject = "order-created",
            DefaultReplyTo = "reply.queue",
            DefaultDataSchema = new Uri("https://example.com/schema"),
            DefaultHeaders = { ["tenant"] = "acme" }
        };
        var context = new AmanhecerContext();
        context.SetMetadata(publication, MetadataName.Publication);
        var message = new Message();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.ContentType!.MediaType).IsEqualTo(publication.DefaultContentType.MediaType);
        await Assert.That(message.Source).IsEqualTo(publication.DefaultSource);
        await Assert.That(message.SpecVersion).IsEqualTo(publication.DefaultSpecVersion);
        await Assert.That(message.Type).IsEqualTo("com.example.order");
        await Assert.That(message.Subject).IsEqualTo("order-created");
        await Assert.That(message.ReplyTo).IsEqualTo("reply.queue");
        await Assert.That(message.DataSchema).IsEqualTo(new Uri("https://example.com/schema"));
        await Assert.That(message.Headers["tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task EncodeAsync_With_Publication_Should_NotOverrideExistingHeaders()
    {
        var publication = new TestPublication
        {
            RoutingKey = "orders",
            DefaultHeaders = { ["tenant"] = "acme", ["trace"] = "default" }
        };
        var context = new AmanhecerContext();
        context.SetMetadata(publication, MetadataName.Publication);
        var message = new Message();
        message.Headers["tenant"] = "contoso";

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.Headers.Count).IsEqualTo(2);
        await Assert.That(message.Headers["tenant"]).IsEqualTo("contoso");
        await Assert.That(message.Headers["trace"]).IsEqualTo("default");
    }

    [Test]
    public async Task EncodeAsync_With_AttributeAndPublication_Should_PreferAttributeValues()
    {
        var attribute = new CloudEventAttribute(0) { Type = "com.example.attribute" };
        var context = new AmanhecerContext();
        context.SetMetadata(attribute);
        var publication = new TestPublication
        {
            RoutingKey = "orders",
            DefaultType = "com.example.publication"
        };
        context.SetMetadata(publication, MetadataName.Publication);
        var message = new Message();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.Type).IsEqualTo("com.example.attribute");
    }

    [Test]
    public async Task EncodeAsync_Without_Metadata_Should_LeaveTheMessageUntouched()
    {
        var message = new Message();
        var context = new AmanhecerContext();
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.EncodeAsync(message, context, next);

        await Assert.That(message.ContentType).IsNull();
        await Assert.That(message.DataSchema).IsNull();
        await Assert.That(message.Source).IsNull();
        await Assert.That(message.Type).IsNull();
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task EncodeAsync_With_InvalidDataSchemaUri_Should_LeaveDataSchemaUnset()
    {
        var attribute = new CloudEventAttribute(0) { DataSchema = "http://exa mple.com/x" };
        var context = new AmanhecerContext();
        context.SetMetadata(attribute);
        var message = new Message();

        await _transformer.EncodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.DataSchema).IsNull();
    }

    [Test]
    public async Task DecodeAsync_With_Subscription_Should_ApplySubscriptionDefaults()
    {
        var subscription = new TestSubscription("orders")
        {
            DefaultType = "com.example.order",
            DefaultSubject = "order-created"
        };
        var context = new AmanhecerContext();
        context.SetMetadata(subscription, MetadataName.Subscription);
        var message = new Message();
        var next = Substitute.For<Func<Message, AmanhecerContext, ValueTask>>();

        await _transformer.DecodeAsync(message, context, next);

        await Assert.That(message.ContentType!.MediaType).IsEqualTo(subscription.DefaultContentType.MediaType);
        await Assert.That(message.Source).IsEqualTo(subscription.DefaultSource);
        await Assert.That(message.SpecVersion).IsEqualTo(subscription.DefaultSpecVersion);
        await Assert.That(message.Type).IsEqualTo("com.example.order");
        await Assert.That(message.Subject).IsEqualTo("order-created");
        await next.Received(1).Invoke(message, context);
    }

    [Test]
    public async Task DecodeAsync_With_PublicationMetadata_Should_NotApplyIt()
    {
        var publication = new TestPublication
        {
            RoutingKey = "orders",
            DefaultType = "com.example.order"
        };
        var context = new AmanhecerContext();
        context.SetMetadata(publication, MetadataName.Publication);
        var message = new Message();

        await _transformer.DecodeAsync(message, context, Substitute.For<Func<Message, AmanhecerContext, ValueTask>>());

        await Assert.That(message.ContentType).IsNull();
        await Assert.That(message.Type).IsNull();
        await Assert.That(message.Source).IsNull();
    }
}
