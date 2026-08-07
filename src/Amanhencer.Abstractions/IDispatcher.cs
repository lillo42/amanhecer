using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

/// <summary>
/// The entry point of Amanhencer: dispatches requests to their pipelines.
/// Use <c>Send</c> for commands with exactly one handler, <c>Publish</c> for events with any
/// number of handlers, and <c>Query</c> for requests that return a response.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Sends a request to the single pipeline registered for it, blocking until it completes.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to send.</typeparam>
    /// <param name="request">The request to send.</param>
    void Send<TRequest>(TRequest request);

    /// <summary>
    /// Sends a request to the single pipeline registered for it, blocking until it completes.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to send.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Send<TRequest>(TRequest request, IContext context);

    /// <summary>
    /// Sends a request to the single pipeline registered for it.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to send.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the single pipeline registered for it.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to send.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask SendAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    void Publish<TRequest>(TRequest request);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Publish<TRequest>(TRequest request, IContext context);

    /// <summary>
    /// Publishes a request to every pipeline registered for it.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask PublishAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a request to every pipeline registered for it.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask PublishAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default);


    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response,
    /// blocking until it completes.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to send.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <returns>The response produced by the query handler.</returns>
    TResponse Query<TQuery, TResponse>(TQuery query);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response,
    /// blocking until it completes.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to send.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <returns>The response produced by the query handler.</returns>
    TResponse Query<TQuery, TResponse>(TQuery query, IContext context);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to send.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to send.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, IContext context, CancellationToken cancellationToken = default);
}