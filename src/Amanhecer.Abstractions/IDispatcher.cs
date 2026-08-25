using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions;

/// <summary>
/// The entry point of Amanhecer: dispatches requests to their pipelines.
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
    /// <param name="request">The request to send.</param>
    void Send(object request);

    /// <summary>
    /// Sends a request to the single pipeline registered for it, blocking until it completes.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to send.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Send<TRequest>(TRequest request, IContext context);

    /// <summary>
    /// Sends a request to the single pipeline registered for it, blocking until it completes.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Send(object request, IContext context);

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
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask SendAsync(object request, CancellationToken cancellationToken = default);

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
    /// Sends a request to the single pipeline registered for it.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask SendAsync(object request, IContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    void Publish<TRequest>(TRequest request);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    void Publish(object request);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request to publish.</typeparam>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Publish<TRequest>(TRequest request, IContext context);

    /// <summary>
    /// Publishes a request to every pipeline registered for it, blocking until all complete.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Publish(object request, IContext context);

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
    /// <param name="request">The request to publish.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask PublishAsync(object request, CancellationToken cancellationToken = default);

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
    /// Publishes a request to every pipeline registered for it.
    /// </summary>
    /// <param name="request">The request to publish.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask PublishAsync(object request, IContext context, CancellationToken cancellationToken = default);

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
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <returns>The response produced by the query handler.</returns>
    TResponse Query<TResponse>(object query);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response,
    /// blocking until it completes.
    /// </summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The response produced by the query handler.</returns>
    object? Query(object query);

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
    /// Sends a query to the single pipeline registered for it and returns the response,
    /// blocking until it completes.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <returns>The response produced by the query handler.</returns>
    TResponse Query<TResponse>(object query, IContext context);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response,
    /// blocking until it completes.
    /// </summary>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <returns>The response produced by the query handler.</returns>
    object? Query(object query, IContext context);

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
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<TResponse> QueryAsync<TResponse>(object query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<object?> QueryAsync(object query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to send.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, IContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<TResponse> QueryAsync<TResponse>(object query, IContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to the single pipeline registered for it and returns the response.
    /// </summary>
    /// <param name="query">The query to send.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced by the query handler.</returns>
    ValueTask<object?> QueryAsync(object query, IContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a message to the single pipeline registered for it (typically a publication
    /// pipeline producing the message to a broker, e.g. when moving it to a dead-letter or
    /// invalid-message queue), blocking until it completes.
    /// </summary>
    /// <typeparam name="T">The type of the message to post.</typeparam>
    /// <param name="message">The message to post.</param>
    void Post<T>(T message);

    /// <summary>
    /// Posts a message to the single pipeline registered for it (typically a publication
    /// pipeline producing the message to a broker, e.g. when moving it to a dead-letter or
    /// invalid-message queue), blocking until it completes.
    /// </summary>
    /// <param name="message">The message to post.</param>
    void Post(Message message);

    /// <summary>
    /// Posts a message to the single pipeline registered for the context's routing key
    /// (typically a publication pipeline producing the message to a broker, e.g. when moving
    /// it to a dead-letter or invalid-message queue), blocking until it completes.
    /// </summary>
    /// <typeparam name="T">The type of the message to post.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Post<T>(T message, IContext context);

    /// <summary>
    /// Posts a message to the single pipeline registered for the context's routing key
    /// (typically a publication pipeline producing the message to a broker, e.g. when moving
    /// it to a dead-letter or invalid-message queue), blocking until it completes.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    void Post(Message message, IContext context);

    /// <summary>
    /// Posts a message to the single pipeline registered for it (typically a publication
    /// pipeline producing the message to a broker, e.g. when moving it to a dead-letter or
    /// invalid-message queue).
    /// </summary>
    /// <typeparam name="T">The type of the message to post.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been posted.</returns>
    ValueTask PostAsync<T>(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a message to the single pipeline registered for it (typically a publication
    /// pipeline producing the message to a broker, e.g. when moving it to a dead-letter or
    /// invalid-message queue).
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been posted.</returns>
    ValueTask PostAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a message to the single pipeline registered for the context's routing key
    /// (typically a publication pipeline producing the message to a broker, e.g. when moving
    /// it to a dead-letter or invalid-message queue).
    /// </summary>
    /// <typeparam name="T">The type of the message to post.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been posted.</returns>
    ValueTask PostAsync<T>(T message, IContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a message to the single pipeline registered for the context's routing key
    /// (typically a publication pipeline producing the message to a broker, e.g. when moving
    /// it to a dead-letter or invalid-message queue).
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <param name="context">Additional context (routing key, metadata, activity, executing strategy) for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been posted.</returns>
    ValueTask PostAsync(Message message, IContext context, CancellationToken cancellationToken = default);
}
