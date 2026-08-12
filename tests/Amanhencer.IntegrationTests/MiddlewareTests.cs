using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

namespace Amanhencer.IntegrationTests;

public class MiddlewareTests : BaseTests
{
    protected override void ConfigureServiceCollection(IServiceCollection services)
    {
        services.AddSingleton<ExecutionLog>();
    }

    protected override void ConfigureAmanhencer(AmanhencerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<OrderedRequestHandler>(routing => routing
                .Use<RecordingMiddleware>(order: 3, metadata: "inner")
                .Use<RecordingMiddleware>(order: 1, metadata: "outer")
                .Use<RecordingMiddleware>(order: 2, metadata: "middle"))
            .AddRequestHandler<MetadataRequestHandler>(routing => routing
                .Use<RecordingMiddleware>(order: 1, metadata: "custom-name"))
            .AddRequestHandler<ShortCircuitRequestHandler>(routing => routing
                .Use<ShortCircuitMiddleware>(order: 1)
                .Use<RecordingMiddleware>(order: 2, metadata: "unreachable"))
            .AddRequestHandler<AttributedRequestHandler>()
            .AddRequestHandler<MergedRequestHandler>(routing => routing
                .Use<RecordingMiddleware>(order: 0, metadata: "fluent"))
            .AddRequestHandler<MethodAttributedRequestHandler>();
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_ExecuteMiddlewaresInDeclaredOrderAroundHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new OrderedRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(
                [
                    "outer:before", "middle:before", "inner:before",
                    "handled:ordered",
                    "inner:after", "middle:after", "outer:after"
                ],
                CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_PassMetadataToMiddlewareInitialize()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new MetadataRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(
                ["custom-name:before", "handled:metadata", "custom-name:after"],
                CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_ShortCircuit_WhenMiddlewareDoesNotCallNext()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new ShortCircuitRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(["short-circuit"], CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_ExecuteAttributeDeclaredMiddlewaresInOrder()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new AttributedRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(
                [
                    "beta:before", "alpha:before",
                    "handled:attributed",
                    "alpha:after", "beta:after"
                ],
                CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_MergeFluentAndAttributeMiddlewaresInOrder()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new MergedRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(
                [
                    "fluent:before", "alpha:before", "beta:before",
                    "handled:merged",
                    "beta:after", "alpha:after", "fluent:after"
                ],
                CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Send_Should_ExecuteMethodDeclaredMiddleware()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new MethodAttributedRequest());

        var log = ServiceProvider.GetRequiredService<ExecutionLog>();
        await Assert.That(log.Entries)
            .IsEquivalentTo(
                ["alpha:before", "handled:method-attributed", "alpha:after"],
                CollectionOrdering.Matching);
    }

    private class ExecutionLog
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyCollection<string> Entries => [.. _entries];

        public void Add(string entry) => _entries.Enqueue(entry);
    }

    private class RecordingMiddleware(ExecutionLog log) : IMiddleware
    {
        private string _name = string.Empty;

        public void Initialize(object? metadata)
        {
            _name = metadata switch
            {
                RecordingAttribute attribute => attribute.Name,
                _ => metadata?.ToString() ?? string.Empty
            };
        }

        public async ValueTask ExecuteAsync(IPipelineContext context,
            Func<IPipelineContext, ValueTask> next)
        {
            log.Add($"{_name}:before");
            await next(context);
            log.Add($"{_name}:after");
        }
    }

    private class ShortCircuitMiddleware(ExecutionLog log) : IMiddleware
    {
        public void Initialize(object? metadata)
        {
        }

        public ValueTask ExecuteAsync(IPipelineContext context,
            Func<IPipelineContext, ValueTask> next)
        {
            log.Add("short-circuit");
            return ValueTask.CompletedTask;
        }
    }

    private abstract class RecordingAttribute(int order, string name) : MiddlewareAttribute(order)
    {
        public string Name { get; } = name;

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public override Type GetMiddlewareType() => typeof(RecordingMiddleware);
    }

    private sealed class AlphaAttribute(int order) : RecordingAttribute(order, "alpha");

    private sealed class BetaAttribute(int order) : RecordingAttribute(order, "beta");

    private record OrderedRequest;

    private class OrderedRequestHandler(ExecutionLog log) : RequestHandler<OrderedRequest>
    {
        public override ValueTask HandleAsync(OrderedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:ordered");
            return ValueTask.CompletedTask;
        }
    }

    private record MetadataRequest;

    private class MetadataRequestHandler(ExecutionLog log) : RequestHandler<MetadataRequest>
    {
        public override ValueTask HandleAsync(MetadataRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:metadata");
            return ValueTask.CompletedTask;
        }
    }

    private record ShortCircuitRequest;

    private class ShortCircuitRequestHandler(ExecutionLog log) : RequestHandler<ShortCircuitRequest>
    {
        public override ValueTask HandleAsync(ShortCircuitRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:short-circuit");
            return ValueTask.CompletedTask;
        }
    }

    private record AttributedRequest;

    [Alpha(2)]
    [Beta(1)]
    private class AttributedRequestHandler(ExecutionLog log) : RequestHandler<AttributedRequest>
    {
        public override ValueTask HandleAsync(AttributedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:attributed");
            return ValueTask.CompletedTask;
        }
    }

    private record MergedRequest;

    [Alpha(1)]
    [Beta(2)]
    private class MergedRequestHandler(ExecutionLog log) : RequestHandler<MergedRequest>
    {
        public override ValueTask HandleAsync(MergedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:merged");
            return ValueTask.CompletedTask;
        }
    }

    private record MethodAttributedRequest;

    private class MethodAttributedRequestHandler(ExecutionLog log) : RequestHandler<MethodAttributedRequest>
    {
        [Alpha(1)]
        public override ValueTask HandleAsync(MethodAttributedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            log.Add("handled:method-attributed");
            return ValueTask.CompletedTask;
        }
    }
}
