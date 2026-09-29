using System;
using System.Reflection;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.InMemory.Configurations;
using Amanhecer.Messaging.Transformers;

namespace Amanhecer.InMemory.Tests;

public class InMemoryPublicationConfiguratorTests
{
    [Test]
    public async Task When_CloudEventType_Is_Json_Should_Append_The_Structured_Envelope_Transformer()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing");
        cfg.CloudEventType(CloudEventType.Json);

        var publication = CreatePublication(cfg);

        var transformers = publication.Transformers;
        await Assert.That(transformers).Count().IsEqualTo(1);
        await Assert
            .That(transformers[0].TransformerType)
            .IsEqualTo(typeof(StructuredCloudEventTransformer));
        await Assert.That(transformers[0].Order).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task When_CloudEventType_Is_Binary_Should_Not_Append_The_Structured_Envelope_Transformer()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing");

        var publication = CreatePublication(cfg);

        await Assert.That(publication.Transformers).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_Transformer_With_Delegate_Should_Append_The_Anonymous_Encode_Transformer()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing");

        Func<
            Message,
            AmanhecerContext,
            Func<Message, AmanhecerContext, ValueTask>,
            ValueTask
        > func = (message, context, next) => next(message, context);
        cfg.Transformer(func, order: 4);

        var publication = CreatePublication(cfg);

        var transformers = publication.Transformers;
        await Assert.That(transformers).Count().IsEqualTo(1);
        await Assert
            .That(transformers[0].TransformerType)
            .IsEqualTo(typeof(AnonymousEncodeTransformer));
        await Assert.That(transformers[0].Order).IsEqualTo(4);
        await Assert.That(transformers[0].Metadata).IsSameReferenceAs(func);
    }

    private static InMemoryPublication CreatePublication(InMemoryPublicationConfigurator cfg)
    {
        var toPublication = typeof(InMemoryPublicationConfigurator).GetMethod(
            "ToPublication",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        return (InMemoryPublication)toPublication.Invoke(cfg, null)!;
    }
}
