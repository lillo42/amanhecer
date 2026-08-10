using System.Collections.Generic;
using System.Threading;
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
    public async Task DeepClone_NoneToken_KeepsOriginalCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var original = TestPipelineContext.Create(cancellationToken: cts.Token);

        var clone = original.DeepClone(cancellationToken: CancellationToken.None);

        await Assert.That(clone.CancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task DeepClone_ExplicitToken_ReplacesCancellationToken()
    {
        using var originalCts = new CancellationTokenSource();
        using var overrideCts = new CancellationTokenSource();
        var original = TestPipelineContext.Create(cancellationToken: originalCts.Token);

        var clone = original.DeepClone(cancellationToken: overrideCts.Token);

        await Assert.That(clone.CancellationToken).IsEqualTo(overrideCts.Token);
    }

    [Test]
    public async Task DeepClone_CopiesTelemetryTagsIntoANewList()
    {
        var original = TestPipelineContext.Create();
        original.TelemetryTags.Add(new KeyValuePair<string, object?>("key", "value"));

        var clone = original.DeepClone();
        clone.TelemetryTags.Add(new KeyValuePair<string, object?>("other", "changed"));

        await Assert.That(ReferenceEquals(original.TelemetryTags, clone.TelemetryTags)).IsFalse();
        await Assert.That(clone.TelemetryTags.Count).IsEqualTo(2);
        await Assert.That(original.TelemetryTags.Count).IsEqualTo(1);
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
