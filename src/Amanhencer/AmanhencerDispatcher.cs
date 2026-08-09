using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Abstractions.Exceptions;

namespace Amanhencer;

/// <summary>
/// The default <see cref="IDispatcher"/> implementation. Resolves the pipeline(s) for a request's
/// routing key through an <see cref="IPipelineFactory"/> and executes them using the
/// <see cref="IExecutingStrategy"/> selected for the context.
/// </summary>
/// <param name="contextFactory">Creates the <see cref="IPipelineContext"/> for each request.</param>
/// <param name="factory">Resolves the pipelines configured for a routing key.</param>
public class AmanhencerDispatcher(IPipelineContextFactory contextFactory, IPipelineFactory factory)
    : IDispatcher
{
    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    public void Send<TRequest>(TRequest request)
    {
        Send(request, new AmanhencerContext());
    }

    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using the provided context.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    public void Send<TRequest>(TRequest request, IContext context)
    {
        var response = SendAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        
        response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sends a request asynchronously to its single matching pipeline, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    public async ValueTask SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, new AmanhencerContext(), cancellationToken);
    }

    /// <summary>
    /// Sends a request asynchronously to its single matching pipeline, using the provided context.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the request's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the request's routing key.</exception>
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
        
        pipelineContext.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhencer.operation", "send"));
        
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

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    public void Publish<TRequest>(TRequest request)
    {
        Publish(request, new AmanhencerContext());
    }

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using the provided context.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    public void Publish<TRequest>(TRequest request, IContext context)
    {
        var response = PublishAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        
        response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Publishes a request asynchronously to all matching pipelines, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have executed.</returns>
    public async ValueTask PublishAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await PublishAsync(request, new AmanhencerContext(), cancellationToken);
    }

    /// <summary>
    /// Publishes a request asynchronously to all pipelines registered for its routing key.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
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

        pipelineContext.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhencer.operation", "post"));
        await pipelineContext.ExecutingStrategy.ExecuteAsync(pipelineContext, pipelines);
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <returns>The response produced by the query handler.</returns>
    public TResponse Query<TQuery, TResponse>(TQuery query)
    {
        return Query<TQuery, TResponse>(query, new AmanhencerContext());
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <returns>The response produced by the query handler.</returns>
    public TResponse Query<TQuery, TResponse>(TQuery query, IContext context)
    {
        var response = QueryAsync<TQuery, TResponse>(query, context);
        if (response.IsCompleted)
        {
            return response.GetAwaiter().GetResult();
        }
        
        return response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhencerContext"/>.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    public async ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<TQuery, TResponse>(query, new AmanhencerContext(), cancellationToken);
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the query's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the query's routing key.</exception>
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
        
        pipelineContext.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhencer.operation", "query"));
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