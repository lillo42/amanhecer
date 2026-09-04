using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Defines a handler that processes a request of type <typeparamref name="TRequest"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request to handle.</typeparam>
public interface IRequestHandler<in TRequest>  : IRequestHandler
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="context">The context of the pipeline executing the request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask  HandleAsync(
        TRequest request,
        AmanhecerContext context,
        CancellationToken cancellationToken = default
    );
    
#if NET8_0_OR_GREATER
    ValueTask IRequestHandler.HandleAsync(
        object request, 
        AmanhecerContext context,
        CancellationToken cancellationToken) 
        => HandleAsync((TRequest)request, context, cancellationToken);
#endif
}


/// <summary>
/// Non-generic <see cref="IRequestHandler"/> used to invoke a request handler without knowing its
/// request type at compile time.
/// </summary>
public interface IRequestHandler : IHandler
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="context">The context of the pipeline executing the request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request has been handled.</returns>
    ValueTask HandleAsync(
        object request,
        AmanhecerContext context,
        CancellationToken cancellationToken = default
    );
}
