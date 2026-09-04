using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using NSubstitute;

namespace Amanhecer.Tests;

public class AmanhecerMiddlewareFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AmanhecerMiddlewareFactory _factory;

    public AmanhecerMiddlewareFactoryTests()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _factory = new AmanhecerMiddlewareFactory(_serviceProvider);
    }

    [Test]
    public async Task When_Create_Should_CreateMiddleware()
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

    [Test]
    public async Task When_CreateWithNullMetadata_Should_InitializeMiddlewareWithNull()
    {
        var middlewareType = typeof(CustomMiddleware);
        var middleware = new CustomMiddleware();

        _serviceProvider.GetService(middlewareType).Returns(middleware);

        await Assert.That(() => _factory.Create(middlewareType, null))
            .ThrowsNothing()
            .And.IsAssignableTo<CustomMiddleware>()
            .And.Member(x => x.Metadata, y => y.IsNull());
    }

    [Test]
    public async Task When_CreateWithUnregisteredMiddleware_Should_ThrowInvalidOperationException()
    {
        var middlewareType = typeof(CustomMiddleware);

        await Assert.That(() => _factory.Create(middlewareType, new object()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task When_CreateWithNonMiddlewareService_Should_ThrowInvalidCastException()
    {
        var middlewareType = typeof(CustomMiddleware);

        _serviceProvider.GetService(middlewareType).Returns(new object());

        await Assert.That(() => _factory.Create(middlewareType, new object()))
            .Throws<InvalidCastException>();
    }
    private class CustomMiddleware : IMiddleware
    {
        public object? Metadata { get; set; }
        public bool Executed { get; private set; }
        public void Initialize(object? metadata)
        {
            Metadata = metadata;
        }

        public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
        {
            Executed = true;
            return new ValueTask();
        }
    }
}