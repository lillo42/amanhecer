using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Base.Tests;

/// <summary>
/// Transport-agnostic contract tests for a messaging gateway: producing, receiving and
/// settling (ack, nack, defer) messages through the <see cref="IProducer"/>/
/// <see cref="IConsumer"/> abstractions.
/// </summary>
/// <remarks>
/// To test a new transport, create a test project that references this one, derive from this
/// class, mark the derived class with <c>[InheritsTests]</c> and implement
/// <see cref="CreateFixtureAsync"/> so it provisions an isolated publication/subscription pair
/// on the transport and returns the producer and consumer bound to it.
/// </remarks>
public abstract class MessagingGatewayTests
{
    /// <summary>
    /// Gets how long to wait for a message that is expected to arrive.
    /// </summary>
    protected virtual TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets how long to wait for a message that is expected <em>not</em> to arrive.
    /// </summary>
    protected virtual TimeSpan NoMessageTimeout => TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Provisions a fresh, isolated publication/subscription pair on the transport under test
    /// and returns the producer and consumer bound to it.
    /// </summary>
    /// <returns>The fixture used by the test; disposed once the test has run.</returns>
    protected abstract Task<MessagingGatewayFixture> CreateFixtureAsync();

    /// <summary>
    /// Creates the message produced by the tests. Override to attach transport-specific
    /// attributes.
    /// </summary>
    /// <returns>A message with a unique id and payload.</returns>
    protected virtual Message CreateMessage()
    {
        return new Message
        {
            ContentType = new ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = Encoding.UTF8.GetBytes(Uuid.NewGuid().ToString())
        };
    }

    /// <summary>
    /// Receives the next message from the fixture's consumer, failing after
    /// <paramref name="timeout"/> when none arrives.
    /// </summary>
    /// <param name="fixture">The fixture whose consumer receives the message.</param>
    /// <param name="timeout">How long to wait for the message.</param>
    /// <returns>The received message.</returns>
    protected static async Task<Message> ReceiveOneAsync(MessagingGatewayFixture fixture, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var messages = await fixture.Consumer.GetMessagesAsync(cts.Token);
        return messages[0];
    }

    [Test]
    public async Task When_Producing_A_Message_Should_Be_Received()
    {
        await using var fixture = await CreateFixtureAsync();
        var message = CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture, ReceiveTimeout);

        await Assert.That(received.Id).IsEqualTo(message.Id);
        await Assert.That(received.CorrelationId).IsEqualTo(message.CorrelationId);
        await Assert.That(received.Payload.Span.SequenceEqual(message.Payload.Span)).IsTrue();

        await fixture.Consumer.AckAsync(received);
    }

    [Test]
    public async Task When_Producing_Multiple_Messages_Should_Receive_All()
    {
        await using var fixture = await CreateFixtureAsync();
        var messages = new[] { CreateMessage(), CreateMessage(), CreateMessage() };

        foreach (var message in messages)
        {
            await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());
        }

        var receivedIds = new List<string>();
        for (var i = 0; i < messages.Length; i++)
        {
            var received = await ReceiveOneAsync(fixture, ReceiveTimeout);
            receivedIds.Add(received.Id);
            await fixture.Consumer.AckAsync(received);
        }

        await Assert.That(receivedIds).Count().IsEqualTo(messages.Length);
        foreach (var message in messages)
        {
            await Assert.That(receivedIds).Contains(message.Id);
        }
    }

    [Test]
    public async Task When_Acking_A_Message_Should_Not_Be_Redelivered()
    {
        await using var fixture = await CreateFixtureAsync();
        var message = CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture, ReceiveTimeout);
        await fixture.Consumer.AckAsync(received);

        await Assert.That(async () => await ReceiveOneAsync(fixture, NoMessageTimeout))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task When_Nacking_A_Message_Should_Not_Be_Redelivered()
    {
        await using var fixture = await CreateFixtureAsync();
        var message = CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture, ReceiveTimeout);
        await fixture.Consumer.NackAsync(received);

        await Assert.That(async () => await ReceiveOneAsync(fixture, NoMessageTimeout))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task When_Deferring_A_Message_Should_Be_Redelivered()
    {
        await using var fixture = await CreateFixtureAsync();
        var message = CreateMessage();

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture, ReceiveTimeout);
        await fixture.Consumer.DeferAsync(received, TimeSpan.FromMilliseconds(50));

        var redelivered = await ReceiveOneAsync(fixture, ReceiveTimeout);

        await Assert.That(redelivered.Id).IsEqualTo(message.Id);
        await Assert.That(redelivered.Payload.Span.SequenceEqual(message.Payload.Span)).IsTrue();

        await fixture.Consumer.AckAsync(redelivered);
    }

    [Test]
    public async Task When_Producing_A_Message_With_Trace_Context_Should_Propagate()
    {
        await using var fixture = await CreateFixtureAsync();
        var message = CreateMessage();
        message.TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        message.TraceState = TraceState.FromString("congo=t61rcWkgMzE");
        message.Baggage = Baggage.FromString("userId=alice,serverNode=DF28");

        await fixture.Producer.ProduceAsync(message, fixture.Publication, new AmanhecerContext());

        var received = await ReceiveOneAsync(fixture, ReceiveTimeout);

        await Assert.That(received.TraceParent).IsEqualTo(message.TraceParent);
        await Assert.That(received.TraceState?.ToString()).IsEqualTo(message.TraceState.ToString());
        await Assert.That(received.Baggage?.ToString()).IsEqualTo(message.Baggage.ToString());

        await fixture.Consumer.AckAsync(received);
    }
}
