using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Metadatas;
using Amanhecer.Middlewares;
using NSubstitute;

namespace Amanhecer.Tests.Middlewares;

public class ExecuteHandlerMiddlewareTests
{
    private readonly IHandlerFactory _factory;
    private readonly ExecuteHandlerMiddleware _middleware;

    public ExecuteHandlerMiddlewareTests()
    {
        _factory = Substitute.For<IHandlerFactory>();
        _middleware = new ExecuteHandlerMiddleware(_factory);
    }

    [Test]
    public async Task When_ExecuteAsyncWithoutHandleTypeMetadata_Should_ThrowInvalidOperationException()
    {
        var context = new AmanhecerContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .Throws<InvalidOperationException>();

        await next.DidNotReceive().Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteRequestHandler()
    {
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        var context = new AmanhecerContext { Request = new SomeRequest() };
        context.SetMetadata(new HandleTypeMetadata(typeof(SomeRequestHandler)));

        var handler = new SomeRequestHandler();
        _factory.Create(typeof(SomeRequestHandler), context)
            .Returns(handler);

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsNothing();

        _factory
            .Received(1)
            .Create(typeof(SomeRequestHandler), context);

        await Assert.That(handler.Executed)
            .IsTrue();

        await next.DidNotReceive().Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ForwardTheCancellationTokenToTheHandler()
    {
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var context = new AmanhecerContext
        {
            Request = new SomeRequest(),
            CancellationToken = cancellationTokenSource.Token
        };
        context.SetMetadata(new HandleTypeMetadata(typeof(SomeRequestHandler)));

        var handler = new SomeRequestHandler();
        _factory.Create(typeof(SomeRequestHandler), context)
            .Returns(handler);

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsNothing();

        await Assert.That(handler.ReceivedCancellationToken)
            .IsEqualTo(cancellationTokenSource.Token);

        await next.DidNotReceive().Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteQueryRequestHandler()
    {
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var message = Guid.NewGuid().ToString();

        var context = new AmanhecerContext { Request = new SomeQueryRequest(message) };
        context.SetMetadata(new HandleTypeMetadata(typeof(SomeQueryRequestHandler)));

        var handler = new SomeQueryRequestHandler();
        _factory.Create(typeof(SomeQueryRequestHandler), context)
            .Returns(handler);

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsNothing();

        _factory
            .Received(1)
            .Create(typeof(SomeQueryRequestHandler), context);

        await Assert.That(handler.Executed)
            .IsTrue();

        await Assert.That(context.Response)
            .IsEqualTo(new SomeQueryResponse(message));

        await next.DidNotReceive().Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ThrowNotSupportedException()
    {
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        var context = new AmanhecerContext();
        context.SetMetadata(new HandleTypeMetadata(typeof(SomeHandler)));

        var handler = new SomeHandler();
        _factory.Create(typeof(SomeHandler), context)
            .Returns(handler);

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .Throws<NotSupportedException>();

        _factory
            .Received(1)
            .Create(typeof(SomeHandler), context);
    }

    private class SomeHandler : IHandler;

    private record SomeRequest;

    private class SomeRequestHandler : RequestHandler<SomeRequest>
    {
        public bool Executed { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public override ValueTask HandleAsync(SomeRequest request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            ReceivedCancellationToken = cancellationToken;
            return new ValueTask();
        }
    }

    private record SomeQueryRequest(string Message);

    private record SomeQueryResponse(string Message);

    private class SomeQueryRequestHandler : QueryHandler<SomeQueryRequest, SomeQueryResponse>
    {
        public bool Executed { get; private set; }

        public override ValueTask<SomeQueryResponse> HandleAsync(SomeQueryRequest query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            return new ValueTask<SomeQueryResponse>(new SomeQueryResponse(query.Message));
        }
    }
}
