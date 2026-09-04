using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer;

/// <summary>
/// The default <see cref="IDispatcher"/> implementation. Resolves the pipeline(s) for a request's
/// routing key through an <see cref="IPipelineFactory"/> and executes them using the
/// <see cref="IExecutingStrategy"/> selected for the context.
/// </summary>
/// <param name="factory">Resolves the pipelines configured for a routing key.</param>
/// <param name="logger">The logger used to record dispatch diagnostics.</param>
public partial class AmanhecerDispatcher(
    IPipelineFactory factory,
    ILogger<AmanhecerDispatcher> logger)
    : IDispatcher
{
    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    public void Send<TRequest>(TRequest request)
    {
        Send(request, new AmanhecerContext());
    }

    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="request">The request to send.</param>
    public void Send(object request)
    {
        Send(request, new AmanhecerContext());
    }

    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using the provided context.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    public void Send<TRequest>(TRequest request, AmanhecerContext context)
    {
        Send((object)request!, context);
    }

    /// <summary>
    /// Sends a request synchronously to its single matching pipeline, using the provided context.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the request's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the request's routing key.</exception>
    public void Send(object request, AmanhecerContext context)
    {
        var response = SendAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        else
        {
            response.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Sends a request asynchronously to its single matching pipeline, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    public async ValueTask SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Sends a request asynchronously to its single matching pipeline, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    public async ValueTask SendAsync(object request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, new AmanhecerContext(), cancellationToken);
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
    public async ValueTask SendAsync<TRequest>(TRequest request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        await SendAsync((object)request!, context, cancellationToken);
    }

    /// <summary>
    /// Sends a request asynchronously to its single matching pipeline, using the provided context.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the request's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the request's routing key.</exception>
    public async ValueTask SendAsync(object request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Request = request;
        var pipelines = factory.Create(context);

        switch (pipelines.Count)
        {
            case 0:
                Logger.NoPipelineFound(logger, context.RoutingKey);
                throw new PipelineNotFoundException(context.RoutingKey);
            case > 1:
                throw new MultiPipelineFoundException(context.RoutingKey);
            default:
                context.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhecer.operation", "send"));
                await context.ExecutingStrategy!.ExecuteAsync(context, pipelines)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
                break;
        }
    }

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    public void Publish<TRequest>(TRequest request)
    {
        Publish(request, new AmanhecerContext());
    }

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    public void Publish(object request)
    {
        Publish(request, new AmanhecerContext());
    }

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using the provided context.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    public void Publish<TRequest>(TRequest request, AmanhecerContext context)
    {
        Publish((object)request!, context);
    }

    /// <summary>
    /// Publishes a request synchronously to all matching pipelines, using the provided context.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
    public void Publish(object request, AmanhecerContext context)
    {
        var response = PublishAsync(request, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        else
        {
            response.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Publishes a request asynchronously to all matching pipelines, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have executed.</returns>
    public async ValueTask PublishAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await PublishAsync(request, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Publishes a request asynchronously to all matching pipelines, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have executed.</returns>
    public async ValueTask PublishAsync(object request, CancellationToken cancellationToken = default)
    {
        await PublishAsync(request, new AmanhecerContext(), cancellationToken);
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
    public async ValueTask PublishAsync<TRequest>(TRequest request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync((object)request!, context, cancellationToken);
    }

    /// <summary>
    /// Publishes a request asynchronously to all pipelines registered for its routing key.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="context"/> is null.</exception>
    public async ValueTask PublishAsync(object request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Request = request;
        var pipelines = factory.Create(context);

        if (pipelines.Count == 0)
        {
            Logger.NoPipelineFound(logger, context.RoutingKey);
            return;
        }

        context.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhecer.operation", "publish"));
        await context
            .ExecutingStrategy!
            .ExecuteAsync(context, pipelines)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <returns>The response produced by the query handler.</returns>
    public TResponse Query<TQuery, TResponse>(TQuery query)
    {
        return Query<TQuery, TResponse>(query, new AmanhecerContext());
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <returns>The response produced by the query handler.</returns>
    public TResponse Query<TResponse>(object query)
    {
        return Query<TResponse>(query, new AmanhecerContext());
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <returns>The response produced by the query handler.</returns>
    public object? Query(object query)
    {
        return Query(query, new AmanhecerContext());
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
    public TResponse Query<TQuery, TResponse>(TQuery query, AmanhecerContext context)
    {
        var response = QueryAsync<TQuery, TResponse>(query, context);
        if (response.IsCompleted)
        {
            return response.GetAwaiter().GetResult();
        }

        return response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <returns>The response produced by the query handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the query's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the query's routing key.</exception>
    public TResponse Query<TResponse>(object query, AmanhecerContext context)
    {
        var response = QueryAsync<TResponse>(query, context);
        if (response.IsCompleted)
        {
            return response.GetAwaiter().GetResult();
        }

        return response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a query synchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <returns>The response produced by the query handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the query's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the query's routing key.</exception>
    public object? Query(object query, AmanhecerContext context)
    {
        var response = QueryAsync(query, context);
        if (response.IsCompleted)
        {
            return response.GetAwaiter().GetResult();
        }

        return response.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    public async ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<TQuery, TResponse>(query, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    public async ValueTask<TResponse> QueryAsync<TResponse>(object query, CancellationToken cancellationToken = default)
    {
        return await QueryAsync<TResponse>(query, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    public async ValueTask<object?> QueryAsync(object query, CancellationToken cancellationToken = default)
    {
        return await QueryAsync(query, new AmanhecerContext(), cancellationToken);
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
    public async ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return await QueryAsync<TResponse>(query!, context, cancellationToken)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the query's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the query's routing key.</exception>
    public async ValueTask<TResponse> QueryAsync<TResponse>(object query, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return (TResponse)(await QueryAsync(query, context, cancellationToken)
            .ConfigureAwait(context.ContinueOnCapturedContext))!;
    }

    /// <summary>
    /// Executes a query asynchronously against its single matching pipeline and returns the handler's
    /// response, using the provided context.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response produced by the query handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the query's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the query's routing key.</exception>
    public async ValueTask<object?> QueryAsync(object query, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Request = query;
        var pipelines = factory.Create(context);

        switch (pipelines.Count)
        {
            case 0:
                Logger.NoPipelineFound(logger, context.RoutingKey);
                throw new PipelineNotFoundException(context.RoutingKey);
            case > 1:
                throw new MultiPipelineFoundException(context.RoutingKey);
            default:
                context.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhecer.operation", "query"));
                await context
                    .ExecutingStrategy!
                    .ExecuteAsync(context, pipelines)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
                return context.Response;
        }
    }

    /// <summary>
    /// Posts a message synchronously to the single publication pipeline registered for it,
    /// using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="message">The message to post.</param>
    public void Post<T>(T message)
    {
        Post(message, new AmanhecerContext());
    }

    /// <summary>
    /// Posts a message synchronously to the single publication pipeline registered for it,
    /// using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="message">The message to post.</param>
    public void Post(Message message)
    {
        Post(message, new AmanhecerContext());
    }

    /// <summary>
    /// Posts a message synchronously to the single publication pipeline registered for the
    /// context's routing key, using the provided context.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the message's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the message's routing key.</exception>
    public void Post<T>(T message, AmanhecerContext context)
    {
        var response = PostCoreAsync(message!, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        else
        {
            response.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Posts a message synchronously to the single publication pipeline registered for the
    /// context's routing key, using the provided context.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the message's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the message's routing key.</exception>
    public void Post(Message message, AmanhecerContext context)
    {
        var response = PostCoreAsync(message, context, CancellationToken.None);
        if (response.IsCompleted)
        {
            response.GetAwaiter().GetResult();
        }
        else
        {
            response.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Posts a message asynchronously to the single publication pipeline registered for it,
    /// using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    public async ValueTask PostAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        await PostAsync(message, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Posts a message asynchronously to the single publication pipeline registered for it,
    /// using a new <see cref="AmanhecerContext"/>.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    public async ValueTask PostAsync(Message message, CancellationToken cancellationToken = default)
    {
        await PostAsync(message, new AmanhecerContext(), cancellationToken);
    }

    /// <summary>
    /// Posts a message asynchronously to the single publication pipeline registered for the
    /// context's routing key, using the provided context.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the message's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the message's routing key.</exception>
    public async ValueTask PostAsync<T>(T message, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        await PostCoreAsync(message!, context, cancellationToken);
    }

    /// <summary>
    /// Posts a message asynchronously to the single publication pipeline registered for the
    /// context's routing key, using the provided context.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="context">The call context (routing key, metadata, executing strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has executed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="PipelineNotFoundException">Thrown when no pipeline is registered for the message's routing key.</exception>
    /// <exception cref="MultiPipelineFoundException">Thrown when more than one pipeline is registered for the message's routing key.</exception>
    public async ValueTask PostAsync(Message message, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        await PostCoreAsync(message, context, cancellationToken);
    }

    private async ValueTask PostCoreAsync(object message, AmanhecerContext context, CancellationToken cancellationToken)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.RoutingKey = "Amanhecer.Messaging.Post";
        context.Request = message;
        var pipelines = factory.Create(context);

        switch (pipelines.Count)
        {
            case 0:
                Logger.NoPipelineFound(logger, context.RoutingKey);
                throw new PipelineNotFoundException(context.RoutingKey);
            case > 1:
                throw new MultiPipelineFoundException(context.RoutingKey);
            default:
                context.TelemetryTags.Add(new KeyValuePair<string, object?>("amanhecer.operation", "post"));
                await context.ExecutingStrategy!
                    .ExecuteAsync(context, pipelines)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
                break;
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning, "No pipeline found for {RoutingKey}")]
        public static partial void NoPipelineFound(ILogger logger, string routingKey);
    }
}