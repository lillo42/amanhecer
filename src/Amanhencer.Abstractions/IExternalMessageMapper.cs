using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

/// <summary>
/// Maps requests of type <typeparamref name="TRequest"/> to and from an <see cref="ExternalMessage"/>,
/// for example when sending requests over a message bus.
/// </summary>
/// <typeparam name="TRequest">The type of the request to map.</typeparam>
public interface IExternalMessageMapper<TRequest>
{
    /// <summary>
    /// Maps the specified request to an <see cref="ExternalMessage"/>.
    /// </summary>
    /// <param name="request">The request to map.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The <see cref="ExternalMessage"/> that represents the request.</returns>
    ValueTask<ExternalMessage> ToMessageAsync(
        TRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Maps the specified <see cref="ExternalMessage"/> back to a request.
    /// </summary>
    /// <param name="message">The message to map.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The request represented by the message.</returns>
    ValueTask<TRequest> ToRequestAsync(
        ExternalMessage message,
        CancellationToken cancellationToken = default
    );
}
