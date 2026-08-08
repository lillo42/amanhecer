using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;

namespace Amanhencer.Tests;

public class PipelineContextTests
{
    [Test]
    public async Task AmanhencerContext_HasEmptyMetadataAndNoRoutingKeyByDefault()
    {
        var context = new AmanhencerContext();

        await Assert.That(context.Metadata).IsEmpty();
        await Assert.That(context.RoutingKey).IsNull();
        await Assert.That(context.ExecutingStrategy).IsNull();
        await Assert.That(context.Activity).IsNull();
    }

    [Test]
    public async Task DeepClone_CopiesAllValues()
    {
        var original = TestPipelineContext.Create(routingKey: "test.key");
        original.Response = "response";

        var clone = original.DeepClone();

        await Assert.That(clone.RoutingKey).IsEqualTo("test.key");
        await Assert.That(ReferenceEquals(original.Request, clone.Request)).IsTrue();
        await Assert.That(ReferenceEquals(original.ExecutingStrategy, clone.ExecutingStrategy)).IsTrue();
        await Assert.That(clone.CancellationToken).IsEqualTo(original.CancellationToken);
        await Assert.That(clone.Response).IsEqualTo("response");
    }

    [Test]
    public async Task DeepClone_CopiesMetadataIntoANewDictionary()
    {
        var original = TestPipelineContext.Create();
        original.Metadata["key"] = "value";

        var clone = original.DeepClone();
        clone.Metadata["other"] = "changed";

        await Assert.That(ReferenceEquals(original.Metadata, clone.Metadata)).IsFalse();
        await Assert.That(clone.Metadata.ContainsKey("key")).IsTrue();
        await Assert.That(original.Metadata.ContainsKey("other")).IsFalse();
    }

    [Test]
    public async Task PipelineNotFoundException_CarriesRoutingKey()
    {
        var exception = new PipelineNotFoundException("missing.key");

        await Assert.That(exception.RoutingKey).IsEqualTo("missing.key");
        await Assert.That(exception.Message.Contains("missing.key")).IsTrue();
        await Assert.That(exception is AmanhencerException).IsTrue();
    }

    [Test]
    public async Task MultiPipelineFoundException_CarriesRoutingKey()
    {
        var exception = new MultiPipelineFoundException("duplicated.key");

        await Assert.That(exception.RoutingKey).IsEqualTo("duplicated.key");
        await Assert.That(exception.Message.Contains("duplicated.key")).IsTrue();
        await Assert.That(exception is AmanhencerException).IsTrue();
    }
}
