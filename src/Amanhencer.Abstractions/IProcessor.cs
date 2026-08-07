using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IProcessor
{
    void Send<TRequest>(TRequest request);
    void Send<TRequest>(TRequest request, IContext context);
    ValueTask SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);
    ValueTask SendAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default);
    
    void Publish<TRequest>(TRequest request);
    void Publish<TRequest>(TRequest request, IContext context);
    ValueTask PublishAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);
    ValueTask PublishAsync<TRequest>(TRequest request, IContext context, CancellationToken cancellationToken = default);
    
    
    TResponse Query<TQuery, TResponse>(TQuery query);
    TResponse Query<TQuery, TResponse>(TQuery query, IContext context);
    ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default);
    ValueTask<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, IContext context, CancellationToken cancellationToken = default);
}