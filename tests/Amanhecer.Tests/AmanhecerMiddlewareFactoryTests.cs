using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
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

        var context = new AmanhecerContext();

        await Assert.That(() => _factory.Create(middlewareType, new CustomMetadata("some-metadata"), context))
            .ThrowsNothing()
            .And.IsSameReferenceAs(middleware);
    }

    [Test]
    public async Task When_Create_Should_StoreMetadataInTheContext()
    {
        var middlewareType = typeof(CustomMiddleware);
        var middleware = new CustomMiddleware();
        _serviceProvider.GetService(middlewareType).Returns(middleware);

        var metadata = new CustomMetadata("some-metadata");
        var context = new AmanhecerContext();

        _factory.Create(middlewareType, metadata, context);

        await Assert.That(context.GetMetadata<CustomMetadata>()).IsSameReferenceAs(metadata);
    }

    [Test]
    public async Task When_CreateWithNullMetadata_Should_NotStoreMetadataInTheContext()
    {
        var middlewareType = typeof(CustomMiddleware);
        var middleware = new CustomMiddleware();

        _serviceProvider.GetService(middlewareType).Returns(middleware);

        var context = new AmanhecerContext();

        await Assert.That(() => _factory.Create(middlewareType, null, context))
            .ThrowsNothing()
            .And.IsSameReferenceAs(middleware);

        await Assert.That(context.Metadata).IsEmpty();
    }

    [Test]
    public async Task When_CreateWithUnregisteredMiddleware_Should_ThrowInvalidOperationException()
    {
        var middlewareType = typeof(CustomMiddleware);

        await Assert.That(() => _factory.Create(middlewareType, new object(), new AmanhecerContext()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task When_CreateWithNonMiddlewareService_Should_ThrowInvalidCastException()
    {
        var middlewareType = typeof(CustomMiddleware);

        _serviceProvider.GetService(middlewareType).Returns(new object());

        await Assert.That(() => _factory.Create(middlewareType, new object(), new AmanhecerContext()))
            .Throws<InvalidCastException>();
    }

    private record CustomMetadata(string Value);

    private class CustomMiddleware : IMiddleware
    {
        public bool Executed { get; private set; }

        public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
        {
            Executed = true;
            return new ValueTask();
        }
    }
}
