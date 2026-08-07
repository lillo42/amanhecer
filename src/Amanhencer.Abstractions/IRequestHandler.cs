using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IRequestHandler<in TRequest>  : IRequestHandler
{
    ValueTask  HandleAsync(
        TRequest request,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    );
    
#if NET8_0_OR_GREATER
    ValueTask IRequestHandler.HandleAsync(
        object request, 
        IPipelineContext context,
        CancellationToken cancellationToken) 
        => HandleAsync((TRequest)request, context, cancellationToken);
#endif
}


public interface IRequestHandler : IHandler
{
    ValueTask HandleAsync(
        object request,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    );
}
