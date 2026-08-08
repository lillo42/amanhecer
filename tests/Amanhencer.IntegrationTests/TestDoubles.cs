using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.IntegrationTests;

public sealed class ExecutionLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Add(string entry) => _entries.Enqueue(entry);

    public string Joined() => string.Join(",", _entries);
}

public static class DispatcherFixture
{
    public static (IDispatcher Dispatcher, ExecutionLog Log) Create(Action<AmanhencerConfigurator> configure)
    {
        var log = new ExecutionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddAmanhencer(configure);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IDispatcher>(), log);
    }

    public static string KeyOf<T>() => typeof(T).FullName!;
}

public record PlaceOrder(string Product);

public class PlaceOrderHandler(ExecutionLog log) : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"handled:{request.Product}");
        return ValueTask.CompletedTask;
    }
}

public class ExplodingOrderHandler : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler exploded.");
    }
}

[RoutingKey("priority.order")]
public record PriorityOrder(string Product);

public class PriorityOrderHandler(ExecutionLog log) : RequestHandler<PriorityOrder>
{
    public override ValueTask HandleAsync(PriorityOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"priority:{request.Product}");
        return ValueTask.CompletedTask;
    }
}

public record GetStock(string Product);

public class GetStockHandler(ExecutionLog log) : QueryHandler<GetStock, int>
{
    public override ValueTask<int> HandleAsync(GetStock @event, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"queried:{@event.Product}");
        return ValueTask.FromResult(5);
    }
}

public record OrderShipped(string Product);

public class FirstShippedHandler(ExecutionLog log) : RequestHandler<OrderShipped>
{
    public override ValueTask HandleAsync(OrderShipped request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"first:{request.Product}");
        return ValueTask.CompletedTask;
    }
}

public class SecondShippedHandler(ExecutionLog log) : RequestHandler<OrderShipped>
{
    public override ValueTask HandleAsync(OrderShipped request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"second:{request.Product}");
        return ValueTask.CompletedTask;
    }
}

public record TenantRequest;

public class TenantRequestHandler(ExecutionLog log) : RequestHandler<TenantRequest>
{
    public override ValueTask HandleAsync(TenantRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"handler-tenant:{context.Metadata.GetValueOrDefault("tenant")}");
        return ValueTask.CompletedTask;
    }
}

public record TenantQuery;

public class TenantQueryHandler : QueryHandler<TenantQuery, string>
{
    public override ValueTask<string> HandleAsync(TenantQuery @event, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(context.Metadata.TryGetValue("tenant", out var tenant) ? (string)tenant : "none");
    }
}

public record AttributedOrder(string Product);

[Audit(1)]
public class AttributedOrderHandler(ExecutionLog log) : RequestHandler<AttributedOrder>
{
    [Timing(2)]
    public override ValueTask HandleAsync(AttributedOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"handled:{request.Product}");
        return ValueTask.CompletedTask;
    }
}

public class OuterMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("outer:before");
        await next(context);
        log.Add("outer:after");
    }
}

public class MiddleMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("middle:before");
        await next(context);
        log.Add("middle:after");
    }
}

public class InnerMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("inner:before");
        await next(context);
        log.Add("inner:after");
    }
}

public class ShortCircuitMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("short-circuit");
        return ValueTask.CompletedTask;
    }
}

public class TenantMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add($"middleware-tenant:{context.Metadata.GetValueOrDefault("tenant")}");
        await next(context);
    }
}

public class ExplodingMiddleware : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        throw new InvalidOperationException("Middleware exploded.");
    }
}

public class AuditMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("audit:before");
        await next(context);
        log.Add("audit:after");
    }
}

public class TimingMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("timing:before");
        await next(context);
        log.Add("timing:after");
    }
}

public sealed class AuditAttribute(int order) : MiddlewareAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType() => typeof(AuditMiddleware);
}

public sealed class TimingAttribute(int order) : MiddlewareAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType() => typeof(TimingMiddleware);
}
