using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public abstract class QueryHandler<TRequest, TResponse> : IQueryHandler<TRequest, TResponse>
{
    public abstract ValueTask<TResponse> HandleAsync(TRequest @event, IPipelineContext context, CancellationToken cancellationToken = default);

    public virtual async ValueTask<object?> HandleAsync(object @event, IPipelineContext context, CancellationToken cancellationToken) 
        => await HandleAsync((TRequest)@event, context, cancellationToken);
}