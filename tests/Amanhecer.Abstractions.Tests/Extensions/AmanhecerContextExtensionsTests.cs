using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Extensions;

namespace Amanhecer.Abstractions.Tests.Extensions;

public class AmanhecerContextExtensionsTests
{
    [Test]
    public async Task SetMetadata_Should_StoreMetadataKeyedByTypeFullName()
    {
        var context = new AmanhecerContext();
        var metadata = new SampleMetadata("value");

        context.SetMetadata(metadata);

        await Assert.That(ReferenceEquals(context.Metadata[typeof(SampleMetadata).FullName!], metadata)).IsTrue();
    }

    [Test]
    public async Task SetMetadata_Null_Should_BeIgnored()
    {
        var context = new AmanhecerContext();

        context.SetMetadata(null);

        await Assert.That(context.Metadata).IsEmpty();
    }

    [Test]
    public async Task SetMetadata_WithKey_Should_StoreUnderGivenKey()
    {
        var context = new AmanhecerContext();

        context.SetMetadata("the-value", "the-key");

        await Assert.That(context.Metadata["the-key"]).IsEqualTo("the-value");
    }

    [Test]
    public async Task SetMetadata_WithKey_Should_OverwriteExistingEntry()
    {
        var context = new AmanhecerContext();
        context.SetMetadata("old", "key");

        context.SetMetadata("new", "key");

        await Assert.That(context.Metadata["key"]).IsEqualTo("new");
    }

    [Test]
    public async Task GetMetadata_Should_ReturnMetadataStoredByType()
    {
        var context = new AmanhecerContext();
        var metadata = new SampleMetadata("value");
        context.SetMetadata(metadata);

        var result = context.GetMetadata<SampleMetadata>();

        await Assert.That(ReferenceEquals(result, metadata)).IsTrue();
    }

    [Test]
    public async Task GetMetadata_MissingEntry_Should_ReturnDefault()
    {
        var context = new AmanhecerContext();

        await Assert.That(context.GetMetadata<SampleMetadata>()).IsNull();
        await Assert.That(context.GetMetadata<int>()).IsEqualTo(0);
    }

    [Test]
    public async Task GetMetadata_WrongTypeUnderKey_Should_ReturnDefault()
    {
        var context = new AmanhecerContext();
        context.SetMetadata("a-string", typeof(int).FullName!);

        var result = context.GetMetadata<int>();

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task GetMetadata_WithKey_Should_ReturnMatchingEntry()
    {
        var context = new AmanhecerContext();
        context.SetMetadata(42, "answer");

        var result = context.GetMetadata<int>("answer");

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task GetRequiredMetadata_Should_ReturnMetadataStoredByType()
    {
        var context = new AmanhecerContext();
        var metadata = new SampleMetadata("value");
        context.SetMetadata(metadata);

        var result = context.GetRequiredMetadata<SampleMetadata>();

        await Assert.That(ReferenceEquals(result, metadata)).IsTrue();
    }

    [Test]
    public async Task GetRequiredMetadata_MissingEntry_Should_ThrowKeyNotFoundException()
    {
        var context = new AmanhecerContext();

        await Assert.That(() => context.GetRequiredMetadata<SampleMetadata>())
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task GetRequiredMetadata_WrongTypeUnderKey_Should_ThrowKeyNotFoundException()
    {
        var context = new AmanhecerContext();
        context.SetMetadata("a-string", "key");

        await Assert.That(() => context.GetRequiredMetadata<int>("key"))
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task GetRequiredMetadata_WithKey_Should_ReturnMatchingEntry()
    {
        var context = new AmanhecerContext();
        context.SetMetadata("stored", "key");

        var result = context.GetRequiredMetadata<string>("key");

        await Assert.That(result).IsEqualTo("stored");
    }

    private sealed record SampleMetadata(string Value);
}
