using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerHandlerFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AmanhencerHandlerFactory _factory;

    public AmanhencerHandlerFactoryTests()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _factory = new AmanhencerHandlerFactory(_serviceProvider);
    }

    [Test]
    [RequiresUnreferencedCode("")]
    public async Task When_Create_Should_CreateHandler()
    {
        var handleType = typeof(CustomHandler);
        var handler = new CustomHandler();
        
        _serviceProvider.GetService(handleType).Returns(handler);
        
        await Assert.That(() => _factory.Create(handleType, Substitute.For<IPipelineContext>()))
            .ThrowsNothing()
            .And.IsEquivalentTo(handler);
    }
    
    private class CustomHandler : IHandler;
}