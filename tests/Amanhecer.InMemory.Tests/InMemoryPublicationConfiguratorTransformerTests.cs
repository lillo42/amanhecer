using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amanhecer.InMemory.Configurations;
using Amanhecer.Messaging.Transformers;

namespace Amanhecer.InMemory.Tests;

public class InMemoryPublicationConfiguratorTransformerTests
{
    [Test]
    public async Task When_Configuring_Json_CloudEvents_Should_Add_The_Structured_Transformer()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing")
            .QueueName("tests.queue")
            .CloudEventType(Abstractions.Messaging.CloudEventType.Json)
            .AdditionalCloudEvent("tenant", "acme");

        var publication = CreatePublication(cfg);

        await Assert
            .That(publication.Transformers.Any(x => x.TransformerType == typeof(StructuredCloudEventTransformer)))
            .IsTrue();
    }

    [Test]
    public async Task When_Configuring_Binary_CloudEvents_Should_Not_Add_The_Structured_Transformer()
    {
        var cfg = new InMemoryPublicationConfigurator();
        cfg.RoutingKey("tests.routing")
            .QueueName("tests.queue")
            .CloudEventType(Abstractions.Messaging.CloudEventType.Binary);

        var publication = CreatePublication(cfg);

        await Assert
            .That(publication.Transformers.Any(x => x.TransformerType == typeof(StructuredCloudEventTransformer)))
            .IsFalse();
    }

    private static InMemoryPublication CreatePublication(InMemoryPublicationConfigurator cfg)
    {
        var toPublication = typeof(InMemoryPublicationConfigurator)
            .GetMethod("ToPublication", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (InMemoryPublication)toPublication.Invoke(cfg, null)!;
    }
}
