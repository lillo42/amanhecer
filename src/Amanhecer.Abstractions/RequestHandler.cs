using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Base class for handlers that process a request of type <typeparamref name="TRequest"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request to handle.</typeparam>
public abstract class RequestHandler<TRequest> : IRequestHandler<TRequest>
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="context">The context of the pipeline executing the request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    public abstract ValueTask HandleAsync(TRequest request, AmanhecerContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the specified request by casting it to <typeparamref name="TRequest"/> and delegating
    /// to <see cref="HandleAsync(TRequest, AmanhecerContext, CancellationToken)"/>.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="context">The context of the pipeline executing the request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    public virtual ValueTask HandleAsync(object request, AmanhecerContext context, CancellationToken cancellationToken)
        => HandleAsync((TRequest)request, context, cancellationToken);
}