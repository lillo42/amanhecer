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
        await Assert.That(entries[0].Message).IsEqualTo($"Processing {routingKey} request {routingKey}");
        await Assert.That(entries[1].Message).IsEqualTo($"Processed {routingKey} request {routingKey}");
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
        await Assert.That(entries[0].Message).IsEqualTo($"Processing {routingKey} request {routingKey}");
        await Assert.That(entries[1].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[1].Message).IsEqualTo($"Failed to process {routingKey} request {routingKey}");
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
        await Assert.That(entries[0].Message).IsEqualTo($"Processing {routingKey} request {routingKey}");
        await Assert.That(entries[1].Message).IsEqualTo($"Processed {routingKey} request {routingKey}");
    }
}
