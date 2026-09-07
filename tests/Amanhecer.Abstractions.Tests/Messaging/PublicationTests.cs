using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class PublicationTests
{
    [Test]
    public async Task Constructor_Should_InitializeDefaults()
    {
        var publication = new TestPublication { RoutingKey = "orders" };

        await Assert.That(publication.RoutingKey).IsEqualTo("orders");
        await Assert.That(publication.DefaultContentType.ToString()).IsEqualTo("text/plain");
        await Assert.That(publication.DefaultSource.ToString()).IsEqualTo("amanhecer");
        await Assert.That(publication.DefaultSpecVersion).IsEqualTo("1.0");
        await Assert.That(publication.CloudEventType).IsEqualTo(CloudEventType.Binary);
        await Assert.That(publication.DefaultType).IsNull();
        await Assert.That(publication.DefaultSubject).IsNull();
        await Assert.That(publication.DefaultReplyTo).IsNull();
        await Assert.That(publication.DefaultDataSchema).IsNull();
        await Assert.That(publication.MessageMapperType).IsNull();
        await Assert.That(publication.Provisioner).IsNull();
        await Assert.That(publication.DefaultHeaders).IsEmpty();
        await Assert.That(publication.AdditionalCloudEvents).IsEmpty();
        await Assert.That(publication.Transformers).IsEmpty();
    }

    [Test]
    public async Task Constructor_Should_GenerateUniqueNamesPerInstance()
    {
        var first = new TestPublication { RoutingKey = "orders" };
        var second = new TestPublication { RoutingKey = "orders" };

        await Assert.That(Guid.TryParse(first.Name, out _)).IsTrue();
        await Assert.That(second.Name).IsNotEqualTo(first.Name);
    }

    [Test]
    public async Task Properties_Should_BeSettable()
    {
        var publication = new TestPublication { RoutingKey = "orders" };

        publication.Name = "my-publication";
        publication.DefaultType = "com.example.order";
        publication.CloudEventType = CloudEventType.Json;
        publication.DefaultHeaders["h"] = "v";
        publication.AdditionalCloudEvents["traceparent"] = "x";

        await Assert.That(publication.Name).IsEqualTo("my-publication");
        await Assert.That(publication.DefaultType).IsEqualTo("com.example.order");
        await Assert.That(publication.CloudEventType).IsEqualTo(CloudEventType.Json);
        await Assert.That(publication.DefaultHeaders["h"]).IsEqualTo("v");
        await Assert.That(publication.AdditionalCloudEvents["traceparent"]).IsEqualTo("x");
    }

    private class TestPublication : Publication;
}
