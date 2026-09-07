using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerMessagePumperFactoryTests
{
    [Test]
    public async Task When_MessagePumperIsRegistered_Should_ReturnTheResolvedPumper()
    {
        var pumper = Substitute.For<IMessagePumper>();
        var provider = new ServiceCollection()
            .AddSingleton(pumper)
            .BuildServiceProvider();
        var factory = new AmanhecerMessagePumperFactory(provider);

        await Assert.That(factory.Create()).IsEqualTo(pumper);
    }

    [Test]
    public async Task When_MessagePumperIsNotRegistered_Should_ThrowInvalidOperationException()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new AmanhecerMessagePumperFactory(provider);

        await Assert.That(() => factory.Create())
            .Throws<InvalidOperationException>();
    }
}
