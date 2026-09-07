using System.Diagnostics;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Extensions;

public class MessageExtensionsTests
{
    [Test]
    public async Task Enrich_NullActivity_Should_LeaveMessageUntouched()
    {
        var message = new Message();

        message.Enrich(null);

        await Assert.That(message.TraceParent).IsNull();
        await Assert.That(message.TraceState).IsNull();
        await Assert.That(message.Baggage).IsNull();
    }

    [Test]
    public async Task Enrich_Should_CopyTraceContextFromActivity()
    {
        using var activity = CreateActivity();
        var message = new Message();

        message.Enrich(activity);

        await Assert.That(message.TraceParent).IsEqualTo(activity.Id);
        await Assert.That(message.TraceState).IsNotNull();
        await Assert.That(message.TraceState!["congo"]).IsEqualTo("ucf=ifqn5fnsiO");
        await Assert.That(message.Baggage).IsNotNull();
        await Assert.That(message.Baggage!["userId"]).IsEqualTo("alice");
    }

    [Test]
    public async Task Enrich_Should_NotOverwriteExistingTraceParent()
    {
        using var activity = CreateActivity();
        var message = new Message { TraceParent = "existing-parent" };

        message.Enrich(activity);

        await Assert.That(message.TraceParent).IsEqualTo("existing-parent");
    }

    [Test]
    public async Task Enrich_Should_NotOverwriteExistingTraceState()
    {
        using var activity = CreateActivity();
        var existing = new TraceState { ["rojo"] = "00f067aa0ba902b7" };
        var message = new Message { TraceState = existing };

        message.Enrich(activity);

        await Assert.That(ReferenceEquals(message.TraceState, existing)).IsTrue();
        await Assert.That(message.TraceState.ContainsKey("congo")).IsFalse();
    }

    [Test]
    public async Task Enrich_NoTraceStateOnActivity_Should_LeaveTraceStateNull()
    {
        using var activity = new Activity("test");
        activity.Start();
        var message = new Message();

        message.Enrich(activity);

        await Assert.That(message.TraceState).IsNull();
    }

    [Test]
    public async Task Enrich_MessageWithoutBaggage_Should_CopyActivityBaggage()
    {
        using var activity = CreateActivity();
        var message = new Message();

        message.Enrich(activity);

        await Assert.That(message.Baggage).IsNotNull();
        await Assert.That(message.Baggage!["userId"]).IsEqualTo("alice");
        await Assert.That(message.Baggage["tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task Enrich_MessageWithBaggage_Should_MergeOnlyMissingKeys()
    {
        using var activity = CreateActivity();
        var message = new Message
        {
            Baggage = new Baggage { ["userId"] = "bob" }
        };

        message.Enrich(activity);

        await Assert.That(message.Baggage["userId"]).IsEqualTo("bob");
        await Assert.That(message.Baggage["tenant"]).IsEqualTo("acme");
    }

    private static Activity CreateActivity()
    {
        var activity = new Activity("test");
        activity.AddBaggage("userId", "alice");
        activity.AddBaggage("tenant", "acme");
        activity.Start();
        activity.TraceStateString = "congo=ucf=ifqn5fnsiO";
        return activity;
    }
}
