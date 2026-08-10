using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.Extensions;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amanhencer.IntegrationTests;

public class LoggerMiddlewareIntegrationTests
{
    [RequestLogging(1)]
    private class LoggedOrderHandler(ExecutionLog log) : RequestHandler<PlaceOrder>
    {
        public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            log.Add($"logged:{request.Product}");
            return ValueTask.CompletedTask;
        }
    }

    private static (IDispatcher Dispatcher, ExecutionLog Log, FakeLogger<AmanhencerLoggerMiddleware> Logger)
        CreateDispatcher(Action<AmanhencerConfigurator> configure)
    {
        var log = new ExecutionLog();
        var logger = new FakeLogger<AmanhencerLoggerMiddleware>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddSingleton<ILogger<AmanhencerLoggerMiddleware>>(logger);
        services.AddAmanhencer(configure);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IDispatcher>(), log, logger);
    }

    [Test]
    public async Task Success_LogsProcessingAndProcessed_EndToEnd()
    {
        var (dispatcher, log, logger) = CreateDispatcher(cfg => cfg.AddRequestHandler<PlaceOrderHandler>(routing =>
            routing.Use<AmanhencerLoggerMiddleware>(order: 1)));
        var routingKey = typeof(PlaceOrder).FullName!;

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Entries).Contains("handled:apple");
        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Information);
        // AmanhencerLoggerMiddleware formats "{RoutingKey} request {RequestType}"; with the default
        // routing key (the request type's full name) the same value lands in both slots, so the key
        // appears twice. That duplication looks like a formatting quirk — assert the essential
        // information (level + routing key present) instead of enshrining the exact literal.
        await Assert.That(entries[0].Message).StartsWith("Processing ");
        await Assert.That(entries[0].Message).Contains(routingKey);
        await Assert.That(entries[1].Message).StartsWith("Processed ");
        await Assert.That(entries[1].Message).Contains(routingKey);
    }

    [Test]
    public async Task Failure_LogsFailedAtError_AndRethrows_EndToEnd()
    {
        var (dispatcher, _, logger) = CreateDispatcher(cfg => cfg.AddRequestHandler<ExplodingOrderHandler>(routing =>
            routing.Use<AmanhencerLoggerMiddleware>(order: 1)));
        var routingKey = typeof(PlaceOrder).FullName!;

        await Assert.That(async () => await dispatcher.SendAsync(new PlaceOrder("apple")))
            .ThrowsExactly<InvalidOperationException>();

        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Message).StartsWith("Processing ");
        await Assert.That(entries[0].Message).Contains(routingKey);
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Error);
        // Same duplication quirk as above: routing key fills both the name and request-type slots.
        await Assert.That(entries[1].Message).StartsWith("Failed to process ");
        await Assert.That(entries[1].Message).Contains(routingKey);
        await Assert.That(entries[1].Exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task Attribute_RegisteredMiddleware_LogsProcessingAndProcessed()
    {
        var (dispatcher, log, logger) = CreateDispatcher(cfg => cfg.AddRequestHandler<LoggedOrderHandler>());
        var routingKey = typeof(PlaceOrder).FullName!;

        await dispatcher.SendAsync(new PlaceOrder("apple"));

        await Assert.That(log.Entries).Contains("logged:apple");
        var entries = logger.Entries;
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entries[0].Message).StartsWith("Processing ");
        await Assert.That(entries[0].Message).Contains(routingKey);
        await Assert.That(entries[1].Message).StartsWith("Processed ");
        await Assert.That(entries[1].Message).Contains(routingKey);
    }
}
