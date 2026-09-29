using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class MessageTests
{
    [Test]
    public async Task Constructor_Should_InitializeDefaults()
    {
        var message = new Message();

        await Assert.That(message.Baggage).IsNull();
        await Assert.That(message.ContentType).IsNotNull();
        await Assert.That(message.DataRef).IsNull();
        await Assert.That(message.DataSchema).IsNull();
        await Assert.That(message.PartitionKey).IsNull();
        await Assert.That(message.ReplyTo).IsNull();
        await Assert.That(message.Subject).IsNull();
        await Assert.That(message.SpecVersion).IsEqualTo(Message.DefaultSpecVersion);
        await Assert.That(message.Source).IsEqualTo(Message.DefaultSource);
        await Assert.That(message.Type).IsEqualTo(Message.DefaultType);
        await Assert.That(message.TraceParent).IsNull();
        await Assert.That(message.TraceState).IsNull();
        await Assert.That(message.Headers).IsEmpty();
        await Assert.That(message.Metadata).IsEmpty();
    }

    [Test]
    public async Task Constructor_Should_GenerateIdAndCorrelationId()
    {
        var message = new Message();

        await Assert.That(Guid.TryParse(message.Id, out _)).IsTrue();
        await Assert.That(Guid.TryParse(message.CorrelationId, out _)).IsTrue();
    }

    [Test]
    public async Task Constructor_Should_GenerateUniqueIdsPerInstance()
    {
        var first = new Message();
        var second = new Message();

        await Assert.That(second.Id).IsNotEqualTo(first.Id);
        await Assert.That(second.CorrelationId).IsNotEqualTo(first.CorrelationId);
    }

    [Test]
    public async Task Constructor_Should_SetTimeToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var message = new Message();

        var after = DateTimeOffset.UtcNow;
        await Assert.That(message.Time >= before).IsTrue();
        await Assert.That(message.Time <= after).IsTrue();
    }
}
