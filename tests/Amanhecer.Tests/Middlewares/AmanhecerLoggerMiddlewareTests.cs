using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Middlewares;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TUnit.Assertions.Enums;

namespace Amanhecer.Tests.Middlewares;

public class AmanhecerLoggerMiddlewareTests
{
    private readonly FakeLogger<AmanhecerLoggerMiddleware> _logger;
    private readonly AmanhecerLoggerMiddleware _middleware;

    public AmanhecerLoggerMiddlewareTests()
    {
        _logger = new FakeLogger<AmanhecerLoggerMiddleware>();
        _middleware = new AmanhecerLoggerMiddleware(_logger);
    }

    [Test]
    public async Task When_ExecuteAsync_WhenCancelled_Should_LogCancelledAndRethrow()
    {
        var context = Substitute.For<IPipelineContext>();
        context.RoutingKey.Returns("orders");
        context.Request.Returns(new SomeRequest());

        var exception = new OperationCanceledException();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        next.Invoke(Arg.Any<IPipelineContext>()).Throws(exception);

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .ThrowsExactly<OperationCanceledException>();

        var entries = _logger.Entries;
        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0])
            .Member(x => x.Level, y => y.IsEqualTo(LogLevel.Information))
            .And.Member(x => x.Message,
                y => y.IsEqualTo($"Processing orders request {typeof(SomeRequest).FullName}"))
            .And.Member(x => x.Exception, y => y.IsNull());
        await Assert.That(entries[1])
            .Member(x => x.Level, y => y.IsEqualTo(LogLevel.Information))
            .And.Member(x => x.Message,
                y => y.IsEqualTo($"Cancelled orders request {typeof(SomeRequest).FullName}"))
            .And.Member(x => x.Exception, y => y.IsEqualTo(exception));
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_ExecuteAsync_Should_BeginScopesForRoutingKeyAndRequestType()
    {
        var context = Substitute.For<IPipelineContext>();
        context.RoutingKey.Returns("orders");
        context.Request.Returns(new SomeRequest());

        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var scopes = _logger.Scopes;
        await Assert.That(scopes)
            .IsEquivalentTo(["orders", typeof(SomeRequest).FullName],
                CollectionOrdering.Matching);
    }

    private sealed record SomeRequest;

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class FakeLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private readonly ConcurrentQueue<string> _scopes = new();

        public IReadOnlyList<LogEntry> Entries => [.. _entries];

        public IReadOnlyList<string> Scopes => [.. _scopes];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            _scopes.Enqueue(state.ToString() ?? string.Empty);
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
