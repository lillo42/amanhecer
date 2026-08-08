using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;

namespace Amanhencer.Tests;

public sealed class ExecutionLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Add(string entry) => _entries.Enqueue(entry);

    public string Joined() => string.Join(",", _entries);
}

public record TestRequest(string Value);

[RoutingKey("routed.request")]
public record RoutedRequest(string Value);

public record AttributedRequest(string Value);

public record TestQuery(int Number);

public class TestRequestHandler(ExecutionLog log) : RequestHandler<TestRequest>
{
    public override ValueTask HandleAsync(TestRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"handled:{request.Value}");
        return ValueTask.CompletedTask;
    }
}

public class SecondTestRequestHandler(ExecutionLog log) : RequestHandler<TestRequest>
{
    public override ValueTask HandleAsync(TestRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"second:{request.Value}");
        return ValueTask.CompletedTask;
    }
}

public class RoutedRequestHandler(ExecutionLog log) : RequestHandler<RoutedRequest>
{
    public override ValueTask HandleAsync(RoutedRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"routed:{request.Value}");
        return ValueTask.CompletedTask;
    }
}

public class FailingRequestHandler : RequestHandler<TestRequest>
{
    public override ValueTask HandleAsync(TestRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler failed.");
    }
}

public class TestQueryHandler : QueryHandler<TestQuery, string>
{
    public override ValueTask<string> HandleAsync(TestQuery @event, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult($"answer:{@event.Number}");
    }
}

public class SecondTestQueryHandler : QueryHandler<TestQuery, string>
{
    public override ValueTask<string> HandleAsync(TestQuery @event, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult($"second:{@event.Number}");
    }
}

[FirstMiddleware(1)]
public class AttributedRequestHandler(ExecutionLog log) : RequestHandler<AttributedRequest>
{
    [SecondMiddleware(2)]
    public override ValueTask HandleAsync(AttributedRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        log.Add($"handled:{request.Value}");
        return ValueTask.CompletedTask;
    }
}

public class FirstMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("first:before");
        await next(context);
        log.Add("first:after");
    }
}

public class SecondMiddleware(ExecutionLog log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("second:before");
        await next(context);
        log.Add("second:after");
    }
}

public sealed class MetadataMiddleware : IMiddleware
{
    public object? ReceivedMetadata { get; private set; }

    public void Initialize(object? metadata) => ReceivedMetadata = metadata;

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next) => next(context);
}

public sealed class FirstMiddlewareAttribute(int order) : MiddlewareAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType() => typeof(FirstMiddleware);
}

public sealed class SecondMiddlewareAttribute(int order) : MiddlewareAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType() => typeof(SecondMiddleware);
}

public sealed class OrderRecordingMiddleware(string name, List<string> log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add($"{name}:before");
        await next(context);
        log.Add($"{name}:after");
    }
}

public sealed class TerminalMiddleware(List<string> log) : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        log.Add("terminal");
        return ValueTask.CompletedTask;
    }
}

public sealed class PassThroughMiddleware : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next) => next(context);
}

public static class TestPipelineContext
{
    public static AmanhencerPipelineContext Create(
        object? request = null,
        string routingKey = "test.key",
        CancellationToken cancellationToken = default)
    {
        return new AmanhencerPipelineContext(
            null,
            [],
            new Dictionary<string, object>(),
            routingKey,
            request ?? new TestRequest("request"),
            new SequenceExecutingStrategy(),
            cancellationToken);
    }
}
