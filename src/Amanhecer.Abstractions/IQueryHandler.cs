using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Defines a handler that processes a query of type <typeparamref name="TQuery"/> and produces a
/// response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TQuery">The type of the query to handle.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IQueryHandler
{
    /// <summary>
    /// Handles the specified query and returns the response.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The context of the pipeline executing the query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced for the query.</returns>
    ValueTask<TResponse> HandleAsync(TQuery query, IPipelineContext context, CancellationToken cancellationToken = default);
    
#if NET8_0_OR_GREATER
    async ValueTask<object?> IQueryHandler.HandleAsync(object query, IPipelineContext context, CancellationToken cancellationToken)
        => await HandleAsync((TQuery)query, context, cancellationToken);
#endif
}

/// <summary>
/// Non-generic <see cref="IQueryHandler"/> used to invoke a query handler without knowing its
/// query and response types at compile time.
/// </summary>
public interface IQueryHandler : IHandler
{
    /// <summary>
    /// Handles the specified query and returns the response.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The context of the pipeline executing the query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The response produced for the query, or <see langword="null"/> if none was set.</returns>
    ValueTask<object?> HandleAsync(object query, IPipelineContext context, CancellationToken cancellationToken = default);
}