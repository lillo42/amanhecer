using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public abstract class RequestHandler<TRequest> : IRequestHandler<TRequest>
{
    public abstract ValueTask HandleAsync(TRequest request, IPipelineContext context, CancellationToken cancellationToken = default);

    public virtual ValueTask HandleAsync(object request, IPipelineContext context, CancellationToken cancellationToken) 
        => HandleAsync((TRequest)request, context, cancellationToken);
}