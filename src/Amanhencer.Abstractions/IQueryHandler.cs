using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IQueryHandler<in TQuery, TResponse> : IQueryHandler
{
    ValueTask<TResponse> HandleAsync(TQuery query, IPipelineContext context, CancellationToken cancellationToken = default);
    
#if NET8_0_OR_GREATER
    async ValueTask<object?> IQueryHandler.HandleAsync(object query, IPipelineContext context, CancellationToken cancellationToken)
        => await HandleAsync((TQuery)query, context, cancellationToken);
#endif
}

public interface IQueryHandler : IHandler
{
    ValueTask<object?> HandleAsync(object query, IPipelineContext context, CancellationToken cancellationToken = default);
}