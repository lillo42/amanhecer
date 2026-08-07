using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;

namespace Amanhencer;

public class AmanhencerProcessor(IPipelineContextFactory contextFactory, IPipelineFactory factory)
    : IProcessor
{
    public void Send<TRequest>(TRequest request)
    {
        Send(request, new AmanhencerContext());
    }

    public void Send<TRequest>(TRequest request, IContext context)
    {
        var response = SendAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        
        response.AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, new AmanhencerContext(), cancellationToken);
    }

    public async ValueTask SendAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var pipelineContext = contextFactory.Create(request, context, cancellationToken);
        var pipelines = factory.Create(pipelineContext);
        switch (pipelines.Count)
        {
            case 0:
                throw new PipelineNotFoundException(pipelineContext.RoutingKey);
            case > 1:
                throw new MultiPipelineFoundException(pipelineContext.RoutingKey);
            default:
                await pipelineContext.ExecutingStrategy.ExecuteAsync(pipelineContext, pipelines);
                break;
        }
    }

    public void Publish<TRequest>(TRequest request)
    {
        Publish(request, new AmanhencerContext());
    }

    public void Publish<TRequest>(TRequest request, IContext context)
    {
        var response = PublishAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        
        response.AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask PublishAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await PublishAsync(request, new AmanhencerContext(), cancellationToken);
    }

    public async ValueTask PublishAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var pipelineContext = contextFactory.Create(request, context, cancellationToken);
        var pipelines = factory.Create(pipelineContext);

        await pipelineContext.ExecutingStrategy.ExecuteAsync(pipelineContext, pipelines);
    }

    public TResponse Query<TQuery, TResponse>(TQuery query)
    {
        return Query<TQuery, TResponse>(query, new AmanhencerContext());
    }

    public TResponse Query<TQuery, TResponse>(TQuery query, IContext context)
    {
        var response = QueryAsync<TQuery, TResponse>(query, context);
        if (response.IsCompleted)
        {
            return response.GetAwaiter().GetResult();
        }
        
        return response.AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<TQuery, TResponse>(query, new AmanhencerContext(), cancellationToken);
    }

    public async ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, IContext context, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var pipelineContext = contextFactory.Create(query, context, cancellationToken);
        var pipelines = factory.Create(pipelineContext);
        switch (pipelines.Count)
        {
            case 0:
                throw new PipelineNotFoundException(pipelineContext.RoutingKey);
            case > 1:
                throw new MultiPipelineFoundException(pipelineContext.RoutingKey);
            default:
                await pipelineContext.ExecutingStrategy.ExecuteAsync(pipelineContext, pipelines);
                return (TResponse)pipelineContext.Response!;
        }
    }
}