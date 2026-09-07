using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using NSubstitute;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class MoveToDeadLetterQueueConsumerActionTests
{
    [Test]
    public async Task ExecuteAsync_NoDeadLetterQueueRoutingKey_Should_DegradeToDefer()
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.DeadLetterQueueRoutingKey.Returns((string?)null);
        var dispatcher = Substitute.For<IDispatcher>();

        var action = await MoveToDeadLetterQueueConsumerAction.Instance
            .ExecuteAsync(new Message(), subscription, dispatcher);

        await Assert.That(ReferenceEquals(action, Defer.Instance)).IsTrue();
        await dispatcher.DidNotReceive()
            .PostAsync(Arg.Any<Message>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithDeadLetterQueueRoutingKey_Should_PostMessageAndAck()
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.DeadLetterQueueRoutingKey.Returns("orders.dlq");
        var dispatcher = Substitute.For<IDispatcher>();
        var message = new Message();

        var action = await MoveToDeadLetterQueueConsumerAction.Instance
            .ExecuteAsync(message, subscription, dispatcher);

        await Assert.That(ReferenceEquals(action, Ack.Instance)).IsTrue();
        await dispatcher.Received(1).PostAsync(
            message,
            Arg.Is<AmanhecerContext>(c => c.RoutingKey == "orders.dlq"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Instance_Should_BeSharedSingleton()
    {
        await Assert.That(ReferenceEquals(MoveToDeadLetterQueueConsumerAction.Instance,
            MoveToDeadLetterQueueConsumerAction.Instance)).IsTrue();
    }
}
