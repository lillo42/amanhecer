using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Options;

namespace Amanhecer.Abstractions.Tests;

public class AmanhecerContextTests
{
    [Test]
    public async Task Constructor_Should_InitializeDefaults()
    {
        var context = new AmanhecerContext();

        await Assert.That(context.RoutingKey).IsEqualTo("");
        await Assert.That(context.TelemetryTags).IsEmpty();
        await Assert.That(context.Metadata).IsEmpty();
        await Assert.That(context.Response).IsNull();
        await Assert.That(context.Activity).IsNull();
        await Assert.That(context.ExecutingStrategy).IsNull();
        await Assert.That(context.Middlewares).IsNull();
        await Assert.That(context.ContinueOnCapturedContext).IsFalse();
    }

    [Test]
    public async Task Constructor_Should_GenerateRequestAndCorrelationIds()
    {
        var context = new AmanhecerContext();

        await Assert.That(Guid.TryParse(context.RequestId, out _)).IsTrue();
        await Assert.That(Guid.TryParse(context.CorrelationId, out _)).IsTrue();
    }

    [Test]
    public async Task Constructor_Should_GenerateUniqueIdsPerInstance()
    {
        var first = new AmanhecerContext();
        var second = new AmanhecerContext();

        await Assert.That(second.RequestId).IsNotEqualTo(first.RequestId);
        await Assert.That(second.CorrelationId).IsNotEqualTo(first.CorrelationId);
    }

    [Test]
    public async Task Clone_Should_CopyScalarProperties()
    {
        var context = new AmanhecerContext
        {
            RoutingKey = "orders",
            ContinueOnCapturedContext = true,
            Request = "the-request",
            Response = "the-response"
        };

        var clone = (AmanhecerContext)context.Clone();

        await Assert.That(clone.RoutingKey).IsEqualTo("orders");
        await Assert.That(clone.ContinueOnCapturedContext).IsTrue();
        await Assert.That(clone.RequestId).IsEqualTo(context.RequestId);
        await Assert.That(clone.CorrelationId).IsEqualTo(context.CorrelationId);
        await Assert.That(clone.CancellationToken).IsEqualTo(context.CancellationToken);
    }

    [Test]
    public async Task Clone_Should_ShareRequestAndResponseReferences()
    {
        var request = new object();
        var response = new object();
        var context = new AmanhecerContext { Request = request, Response = response };

        var clone = (AmanhecerContext)context.Clone();

        await Assert.That(ReferenceEquals(clone.Request, request)).IsTrue();
        await Assert.That(ReferenceEquals(clone.Response, response)).IsTrue();
    }

    [Test]
    public async Task Clone_Should_CopyCollectionsIntoNewInstances()
    {
        var context = new AmanhecerContext
        {
            Request = new object(),
            TelemetryTags = [new KeyValuePair<string, object?>("key", "value")],
            Metadata = { ["meta"] = 42 },
            Middlewares = [new AmanhecerMiddlewareOptions(typeof(object), 1, null)]
        };

        var clone = (AmanhecerContext)context.Clone();

        await Assert.That(clone.TelemetryTags.Count).IsEqualTo(1);
        await Assert.That(clone.Metadata["meta"]).IsEqualTo(42);
        await Assert.That(clone.Middlewares!.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(clone.TelemetryTags, context.TelemetryTags)).IsFalse();
        await Assert.That(ReferenceEquals(clone.Metadata, context.Metadata)).IsFalse();
        await Assert.That(ReferenceEquals(clone.Middlewares, context.Middlewares)).IsFalse();
    }

    [Test]
    public async Task Clone_MutatingCloneCollections_Should_NotAffectOriginal()
    {
        var context = new AmanhecerContext { Request = new object() };
        context.Metadata["original"] = true;

        var clone = (AmanhecerContext)context.Clone();
        clone.Metadata["added"] = true;
        clone.Metadata.Remove("original");
        clone.TelemetryTags.Add(new KeyValuePair<string, object?>("k", "v"));

        await Assert.That(context.Metadata.ContainsKey("original")).IsTrue();
        await Assert.That(context.Metadata.ContainsKey("added")).IsFalse();
        await Assert.That(context.TelemetryTags).IsEmpty();
    }

    [Test]
    public async Task Clone_NullMiddlewares_Should_StayNull()
    {
        var context = new AmanhecerContext { Request = new object(), Middlewares = null };

        var clone = (AmanhecerContext)context.Clone();

        await Assert.That(clone.Middlewares).IsNull();
    }
}
