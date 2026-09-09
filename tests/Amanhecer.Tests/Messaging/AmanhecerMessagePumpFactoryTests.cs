using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerMessagePumpFactoryTests
{
    [Test]
    public async Task When_MessagePumpIsRegistered_Should_ReturnTheResolvedPump()
    {
        var pump = Substitute.For<IMessagePump>();
        var provider = new ServiceCollection()
            .AddSingleton(pump)
            .BuildServiceProvider();
        var factory = new AmanhecerMessagePumpFactory(provider);

        await Assert.That(factory.Create()).IsEqualTo(pump);
    }

    [Test]
    public async Task When_MessagePumpIsNotRegistered_Should_ThrowInvalidOperationException()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new AmanhecerMessagePumpFactory(provider);

        await Assert.That(() => factory.Create())
            .Throws<InvalidOperationException>();
    }
}
