using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using NSubstitute;

namespace Amanhecer.Tests;

public class AmanhecerHandlerFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AmanhecerHandlerFactory _factory;

    public AmanhecerHandlerFactoryTests()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _factory = new AmanhecerHandlerFactory(_serviceProvider);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Object equivalency uses structural comparison, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Create_Should_CreateHandler()
    {
        var handleType = typeof(CustomHandler);
        var handler = new CustomHandler();
        _serviceProvider.GetService(handleType).Returns(handler);
        await Assert.That(() => _factory.Create(handleType, new AmanhecerContext()))
            .ThrowsNothing()
            .And.IsEquivalentTo(handler);
    }

    [Test]
    public async Task When_CreateWithUnregisteredHandler_Should_ThrowInvalidOperationException()
    {
        var handleType = typeof(CustomHandler);

        await Assert.That(() => _factory.Create(handleType, new AmanhecerContext()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task When_CreateWithNonHandlerService_Should_ThrowInvalidCastException()
    {
        var handleType = typeof(CustomHandler);

        _serviceProvider.GetService(handleType).Returns(new object());

        await Assert.That(() => _factory.Create(handleType, new AmanhecerContext()))
            .Throws<InvalidCastException>();
    }

    private class CustomHandler : IHandler;
}