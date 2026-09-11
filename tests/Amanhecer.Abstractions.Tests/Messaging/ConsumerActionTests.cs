using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class ConsumerActionTests
{
    [Test]
    public async Task Ack_Instance_Should_BeSharedSingleton()
    {
        await Assert.That(ReferenceEquals(Ack.Instance, Ack.Instance)).IsTrue();
        await Assert.That(Ack.Instance is IConsumerAction).IsTrue();
    }

    [Test]
    public async Task Nack_Instance_Should_BeSharedSingleton()
    {
        await Assert.That(ReferenceEquals(Nack.Instance, Nack.Instance)).IsTrue();
        await Assert.That(Nack.Instance is IConsumerAction).IsTrue();
    }

    [Test]
    public async Task Defer_Should_StoreDelay()
    {
        var defer = new Defer(TimeSpan.FromSeconds(3));

        await Assert.That(defer.Delay).IsEqualTo(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task Defer_Instance_Should_HaveZeroDelay()
    {
        await Assert.That(Defer.Instance.Delay).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task Defer_Should_HaveValueEquality()
    {
        await Assert.That(new Defer(TimeSpan.FromSeconds(3))).IsEqualTo(new Defer(TimeSpan.FromSeconds(3)));
        await Assert.That(new Defer(TimeSpan.FromSeconds(3))).IsNotEqualTo(new Defer(TimeSpan.FromSeconds(4)));
    }

    [Test]
    public async Task NackException_Should_BeAnAmanhecerException()
    {
        await Assert.That(new NackException() is AmanhecerException).IsTrue();
    }

    [Test]
    public async Task DeferException_DefaultConstructor_Should_HaveZeroDelay()
    {
        var exception = new DeferException();

        await Assert.That(exception.Delay).IsEqualTo(TimeSpan.Zero);
        await Assert.That(exception is AmanhecerException).IsTrue();
    }

    [Test]
    public async Task DeferException_Should_StoreDelay()
    {
        var exception = new DeferException(TimeSpan.FromSeconds(7));

        await Assert.That(exception.Delay).IsEqualTo(TimeSpan.FromSeconds(7));
    }
}
