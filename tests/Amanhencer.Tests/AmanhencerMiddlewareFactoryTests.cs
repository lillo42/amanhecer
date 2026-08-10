using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerMiddlewareFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AmanhencerMiddlewareFactory _factory;

    public AmanhencerMiddlewareFactoryTests()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _factory = new AmanhencerMiddlewareFactory(_serviceProvider);
    }

    
    [Test]
    [RequiresUnreferencedCode("")]
    public async Task When_Create_Should_CreateHandler()
    {
        var middlewareType = typeof(CustomMiddleware);
        var middleware = new CustomMiddleware();
        
        _serviceProvider.GetService(middlewareType).Returns(middleware);

        var metadata = new object(); 
        await Assert.That(() => _factory.Create(middlewareType, metadata))
            .ThrowsNothing()
            .And.IsAssignableTo<CustomMiddleware>()
            .And.Member(x => x.Metadata, y => y.IsEqualTo(metadata))
            .And.Member(x => x.Executed, y => y.IsFalse());
    }
    
    private class CustomMiddleware : IMiddleware
    {
        public object? Metadata { get; set; }
        public bool Executed { get; private set; }
        
        public void Initialize(object? metadata)
        {
            Metadata = metadata;
        }
        

        public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
        {
            Executed = true;
            return new ValueTask();
        }
    }
}