using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using NSubstitute;

namespace Amanhencer.Tests.Middlewares;

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
    public async Task When_Initialize_Should_SetMetadata()
    {
        await Assert.That(() => _middleware.Initialize(typeof(SomeRequestHandler)))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Initialize_Should_ThrowNullReferenceException()
    {
        await Assert.That(() => _middleware.Initialize(null))
            .Throws<NullReferenceException>();
    }

    [Test]
    public async Task When_Initialize_Should_ThrowNotSupportedException()
    {
        await Assert.That(() => _middleware.Initialize(""))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ThrowNullReferenceException()
    {
        var context = Substitute.For<IPipelineContext>();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .Throws<NullReferenceException>();

        await next.DidNotReceive().Invoke(context);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteRequestHandler()
    {
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        var context = Substitute.For<IPipelineContext>();
        context.Request.Returns(new SomeRequest());

        var handler = new SomeRequestHandler();
        _factory.Create(typeof(SomeRequestHandler), context)
            .Returns(handler);

        _middleware.Initialize(typeof(SomeRequestHandler));
        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsNothing();

        _factory
            .Received(1)
            .Create(typeof(SomeRequestHandler), context);

        await Assert.That(handler.Executed)
            .IsTrue();
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ExecuteQueryRequestHandler()
    {
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        var message = Guid.NewGuid().ToString();

        var context = Substitute.For<IPipelineContext>();
        context.Request.Returns(new SomeQueryRequest(message));

        var handler = new SomeQueryRequestHandler();
        _factory.Create(typeof(SomeQueryRequestHandler), context)
            .Returns(handler);

        _middleware.Initialize(typeof(SomeQueryRequestHandler));
        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsNothing();

        _factory
            .Received(1)
            .Create(typeof(SomeQueryRequestHandler), context);

        await Assert.That(handler.Executed)
            .IsTrue();

        context.Received(1).Response = new SomeQueryResponse(message);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_ThrowNotSupportedException()
    {
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        var context = Substitute.For<IPipelineContext>();

        var handler = new SomeHandler();
        _factory.Create(typeof(SomeHandler), context)
            .Returns(handler);

        _middleware.Initialize(typeof(SomeHandler));
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

        public override ValueTask HandleAsync(SomeRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            return new ValueTask();
        }
    }

    private record SomeQueryRequest(string Message);

    private record SomeQueryResponse(string Message);

    private class SomeQueryRequestHandler : QueryHandler<SomeQueryRequest, SomeQueryResponse>
    {
        public bool Executed { get; private set; }

        public override ValueTask<SomeQueryResponse> HandleAsync(SomeQueryRequest query, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            return new ValueTask<SomeQueryResponse>(new SomeQueryResponse(query.Message));
        }
    }
}