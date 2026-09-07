using System;
using System.Threading.Tasks;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerMessageMapperFactoryTests
{
    [Test]
    public async Task When_MapperTypeRegistered_Should_ResolveItFromTheServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<AttributedMapper>();
        var factory = new AmanhecerMessageMapperFactory(services.BuildServiceProvider());

        var mapper = factory.Create(typeof(AttributedMapper));

        await Assert.That(mapper).IsTypeOf<AttributedMapper>();
    }

    [Test]
    public async Task When_MapperTypeNotRegistered_Should_ThrowInvalidOperationException()
    {
        var factory = new AmanhecerMessageMapperFactory(new ServiceCollection().BuildServiceProvider());

        await Assert.That(() => factory.Create(typeof(AttributedMapper)))
            .Throws<InvalidOperationException>();
    }
}
