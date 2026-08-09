using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Middlewares;
using Microsoft.Extensions.Logging;

namespace Amanhencer.Tests;

public class LoggerMiddlewareTests
{
    private const string RoutingKey = "test.key";

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, IReadOnlyList<object?> Scopes);

    private sealed class FakeLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _entries = [];
        private readonly List<object?> _activeScopes = [];

        public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            _activeScopes.Add(state);
            return new Scope(this, state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, _activeScopes.ToArray()));
        }

        private sealed class Scope(FakeLogger<T> logger, object? state) : IDisposable
        {
            public void Dispose() => logger._activeScopes.Remove(state);
        }
    }

    private static string RequestType => typeof(TestRequest).FullName!;

    private static bool HasScopeValue(LogEntry entry, string key, object? value)
    {
        return entry.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .Any(pair => pair.Key == key && Equals(pair.Value, value));
    }

    [Test]
    public async Task Success_LogsProcessingBeforeNext_AndProcessedAfter()
    {
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var middleware = new AmanhencerLoggerMiddleware(logger);
        var entriesWhenHandlerRan = -1;

        await middleware.ExecuteAsync(TestPipelineContext.Create(routingKey: RoutingKey), _ =>
        {
            entriesWhenHandlerRan = logger.Entries.Count;
            return ValueTask.CompletedTask;
        });

        var entries = logger.Entries;
        await Assert.That(entriesWhenHandlerRan).IsEqualTo(1);
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[0].Message).IsEqualTo($"Processing {RoutingKey} request {RequestType}");
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[1].Message).IsEqualTo($"Processed {RoutingKey} request {RequestType}");
    }

    [Test]
    public async Task Failure_LogsFailedAtError_AndRethrows_AndSkipsProcessed()
    {
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var middleware = new AmanhencerLoggerMiddleware(logger);

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: RoutingKey),
                _ => throw new InvalidOperationException("Boom.")))
            .ThrowsExactly<InvalidOperationException>();

        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Message).IsEqualTo($"Processing {RoutingKey} request {RequestType}");
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[1].Message).IsEqualTo($"Failed to process {RoutingKey} request {RequestType}");
        await Assert.That(entries[1].Exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task Cancelled_LogsCancelledAtInformation_AndRethrows()
    {
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var middleware = new AmanhencerLoggerMiddleware(logger);

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(routingKey: RoutingKey),
                _ => throw new OperationCanceledException("Cancelled.")))
            .ThrowsExactly<OperationCanceledException>();

        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[1].Message).IsEqualTo($"Cancelled {RoutingKey} request {RequestType}");
        await Assert.That(entries[1].Exception is OperationCanceledException).IsTrue();
    }

    [Test]
    public async Task Entries_AreScopedWithRoutingKeyAndRequestType()
    {
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var middleware = new AmanhencerLoggerMiddleware(logger);

        await middleware.ExecuteAsync(TestPipelineContext.Create(routingKey: RoutingKey), _ => ValueTask.CompletedTask);

        foreach (var entry in logger.Entries)
        {
            await Assert.That(HasScopeValue(entry, "RoutingKey", RoutingKey)).IsTrue();
            await Assert.That(HasScopeValue(entry, "RequestType", RequestType)).IsTrue();
        }
    }

    [Test]
    public async Task Attribute_ReturnsLoggerMiddlewareType_AndKeepsOrder()
    {
        var attribute = new RequestLoggingAttribute(5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(AmanhencerLoggerMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
    }
}
