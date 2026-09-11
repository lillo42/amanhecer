using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using NSubstitute;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class MoveToInvalidConsumerActionTests
{
    [Test]
    public async Task ExecuteAsync_NoInvalidMessageRoutingKey_Should_DegradeToNack()
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.InvalidMessageRoutingKey.Returns((string?)null);
        var dispatcher = Substitute.For<IDispatcher>();

        var action = await MoveToInvalidConsumerAction.Instance
            .ExecuteAsync(new Message(), subscription, dispatcher);

        await Assert.That(ReferenceEquals(action, Nack.Instance)).IsTrue();
        await dispatcher.DidNotReceive()
            .PostAsync(Arg.Any<Message>(), Arg.Any<AmanhecerContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidMessageRoutingKey_Should_PostMessageAndAck()
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.InvalidMessageRoutingKey.Returns("orders.invalid");
        var dispatcher = Substitute.For<IDispatcher>();
        var message = new Message();

        var action = await MoveToInvalidConsumerAction.Instance
            .ExecuteAsync(message, subscription, dispatcher);

        await Assert.That(ReferenceEquals(action, Ack.Instance)).IsTrue();
        await dispatcher.Received(1).PostAsync(
            message,
            Arg.Is<AmanhecerContext>(c => c.RoutingKey == "orders.invalid"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Instance_Should_BeSharedSingleton()
    {
        await Assert.That(ReferenceEquals(MoveToInvalidConsumerAction.Instance,
            MoveToInvalidConsumerAction.Instance)).IsTrue();
    }
}
