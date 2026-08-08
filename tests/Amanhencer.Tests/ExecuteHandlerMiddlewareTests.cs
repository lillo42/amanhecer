using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using NSubstitute;

namespace Amanhencer.Tests;

public class ExecuteHandlerMiddlewareTests
{
    private static IHandlerFactory FactoryReturning(IHandler handler)
    {
        var factory = Substitute.For<IHandlerFactory>();
        factory.Create(Arg.Any<Type>(), Arg.Any<IPipelineContext>()).Returns(handler);
        return factory;
    }

    private static ValueTask Next(IPipelineContext context) => ValueTask.CompletedTask;

    [Test]
    public async Task Initialize_NullMetadata_ThrowsNullReferenceException()
    {
        var middleware = new ExecuteHandlerMiddleware(Substitute.For<IHandlerFactory>());

        await Assert.That(() => middleware.Initialize(null))
            .ThrowsExactly<NullReferenceException>();
    }

    [Test]
    public async Task Initialize_NonTypeMetadata_ThrowsNotSupportedException()
    {
        var middleware = new ExecuteHandlerMiddleware(Substitute.For<IHandlerFactory>());

        await Assert.That(() => middleware.Initialize("not-a-type"))
            .ThrowsExactly<NotSupportedException>();
    }

    [Test]
    public async Task ExecuteAsync_NotInitialized_ThrowsNullReferenceException()
    {
        var middleware = new ExecuteHandlerMiddleware(Substitute.For<IHandlerFactory>());

        await Assert.That(async () => await middleware.ExecuteAsync(TestPipelineContext.Create(), Next))
            .ThrowsExactly<NullReferenceException>();
    }

    [Test]
    public async Task ExecuteAsync_ExecutesRequestHandler_AndEndsPipeline()
    {
        var handler = Substitute.For<IRequestHandler>();
        var factory = FactoryReturning(handler);
        var middleware = new ExecuteHandlerMiddleware(factory);
        middleware.Initialize(typeof(TestRequestHandler));
        var context = TestPipelineContext.Create(new TestRequest("hello"));
        var nextCalled = false;

        await middleware.ExecuteAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        });

        factory.Received(1).Create(typeof(TestRequestHandler), context);
        _ = handler.Received(1).HandleAsync(context.Request, context, context.CancellationToken);
        await Assert.That(nextCalled).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_QueryHandler_SetsContextResponse()
    {
        var handler = Substitute.For<IQueryHandler>();
        handler.HandleAsync(Arg.Any<object>(), Arg.Any<IPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>("answer:7"));
        var middleware = new ExecuteHandlerMiddleware(FactoryReturning(handler));
        middleware.Initialize(typeof(TestQueryHandler));
        var context = TestPipelineContext.Create(new TestQuery(7));

        await middleware.ExecuteAsync(context, Next);

        _ = handler.Received(1).HandleAsync(context.Request, context, context.CancellationToken);
        await Assert.That(context.Response).IsEqualTo("answer:7");
    }

    [Test]
    public async Task ExecuteAsync_UnsupportedHandler_ThrowsNotSupportedException()
    {
        var middleware = new ExecuteHandlerMiddleware(FactoryReturning(Substitute.For<IHandler>()));
        middleware.Initialize(typeof(IHandler));

        await Assert.That(async () => await middleware.ExecuteAsync(TestPipelineContext.Create(), Next))
            .ThrowsExactly<NotSupportedException>();
    }
}
