using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class SubscriptionTests
{
    [Test]
    public async Task Constructor_Should_StoreToRoutingKey()
    {
        var subscription = new TestSubscription("orders.created");

        await Assert.That(subscription.ToRoutingKey).IsEqualTo("orders.created");
    }

    [Test]
    public async Task Constructor_Should_InitializeDefaults()
    {
        var subscription = new TestSubscription("orders");

        await Assert.That(subscription.NumberOfConsumers).IsEqualTo(1);
        await Assert.That(subscription.BufferSize).IsEqualTo(1);
        await Assert.That(subscription.NoMessageDelay).IsEqualTo(TimeSpan.FromMilliseconds(300));
        await Assert.That(subscription.FailureDelay).IsEqualTo(TimeSpan.FromMilliseconds(300));
        await Assert.That(subscription.ReceiveMessageTimeout).IsEqualTo(TimeSpan.FromMilliseconds(300));
        await Assert.That(subscription.DefaultContentType.ToString()).IsEqualTo("text/plain");
        await Assert.That(subscription.DefaultSpecVersion).IsEqualTo("1.0");
        await Assert.That(subscription.DefaultSource.ToString()).IsEqualTo("amanhecer");
        await Assert.That(subscription.DefaultType).IsEqualTo("default");
        await Assert.That(subscription.MessagingSystem).IsEqualTo("amanhecer");
        await Assert.That(subscription.DefaultSubject).IsNull();
        await Assert.That(subscription.DefaultReplyTo).IsNull();
        await Assert.That(subscription.DefaultDataSchema).IsNull();
        await Assert.That(subscription.MessageMapperType).IsNull();
        await Assert.That(subscription.Provisioner).IsNull();
        await Assert.That(subscription.DeadLetterQueueRoutingKey).IsNull();
        await Assert.That(subscription.InvalidMessageRoutingKey).IsNull();
        await Assert.That(subscription.ContinueOnCapturedContext).IsFalse();
    }

    [Test]
    public async Task Constructor_Should_GenerateUniqueNamesPerInstance()
    {
        var first = new TestSubscription("orders");
        var second = new TestSubscription("orders");

        await Assert.That(Guid.TryParse(first.Name, out _)).IsTrue();
        await Assert.That(second.Name).IsNotEqualTo(first.Name);
    }

    [Test]
    public async Task OnError_InvalidMessageException_Should_ReturnMoveToInvalidConsumerAction()
    {
        var subscription = new TestSubscription("orders");

        var action = subscription.OnError(new Message(), new InvalidMessageException());

        await Assert.That(ReferenceEquals(action, MoveToInvalidConsumerAction.Instance)).IsTrue();
    }

    [Test]
    public async Task OnError_OtherException_Should_ReturnDeferWithFiveSecondDelay()
    {
        var subscription = new TestSubscription("orders");

        var action = subscription.OnError(new Message(), new InvalidOperationException("boom"));

        var defer = action as Defer;
        await Assert.That(defer).IsNotNull();
        await Assert.That(defer!.Delay).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    private class TestSubscription(string toRoutingKey) : Subscription(toRoutingKey);
}
