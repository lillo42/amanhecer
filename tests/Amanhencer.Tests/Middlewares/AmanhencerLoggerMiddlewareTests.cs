using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Amanhencer.Tests.Middlewares;

public class AmanhencerLoggerMiddlewareTests
{
    private readonly FakeLogger<AmanhencerLoggerMiddleware> _logger;
    private readonly AmanhencerLoggerMiddleware _middleware;

    public AmanhencerLoggerMiddlewareTests()
    {
        _logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        _middleware = new AmanhencerLoggerMiddleware(_logger);
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
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[0].Message)
            .IsEqualTo($"Processing orders request {typeof(SomeRequest).FullName}");
        await Assert.That(entries[0].Exception).IsNull();
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[1].Message)
            .IsEqualTo($"Cancelled orders request {typeof(SomeRequest).FullName}");
        await Assert.That(entries[1].Exception).IsEqualTo(exception);
    }

    [Test]
    public async Task When_ExecuteAsync_Should_BeginScopesForRoutingKeyAndRequestType()
    {
        var context = Substitute.For<IPipelineContext>();
        context.RoutingKey.Returns("orders");
        context.Request.Returns(new SomeRequest());

        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        var scopes = _logger.Scopes;
        await Assert.That(scopes.Count).IsEqualTo(2);
        await Assert.That(scopes[0]).IsEqualTo("orders");
        await Assert.That(scopes[1]).IsEqualTo(typeof(SomeRequest).FullName);
    }

    private sealed record SomeRequest;

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class FakeLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private readonly ConcurrentQueue<string> _scopes = new();

        public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

        public IReadOnlyList<string> Scopes => _scopes.ToArray();

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
