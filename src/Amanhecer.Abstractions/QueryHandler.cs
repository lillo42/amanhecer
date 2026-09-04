using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Base class for handlers that process a query of type <typeparamref name="TRequest"/> and
/// produce a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the query to handle.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the handler.</typeparam>
public abstract class QueryHandler<TRequest, TResponse> : IQueryHandler<TRequest, TResponse>
{
    /// <summary>
    /// Handles the specified query and returns the response.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The context of the pipeline executing the query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced for the query.</returns>
    public abstract ValueTask<TResponse> HandleAsync(TRequest query, AmanhecerContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the specified query by casting it to <typeparamref name="TRequest"/> and delegating
    /// to <see cref="HandleAsync(TRequest, AmanhecerContext, CancellationToken)"/>.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The context of the pipeline executing the query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced for the query, or <see langword="null"/> if none was set.</returns>
    public virtual async ValueTask<object?> HandleAsync(object query, AmanhecerContext context, CancellationToken cancellationToken)
        => await HandleAsync((TRequest)query, context, cancellationToken);
}