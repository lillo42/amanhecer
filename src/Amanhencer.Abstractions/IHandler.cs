namespace Amanhencer.Abstractions;

/// <summary>
/// Marker interface for all handler types. Use <see cref="IRequestHandler{TRequest}"/> or
/// <see cref="IQueryHandler{TQuery,TResponse}"/> to implement a handler.
/// </summary>
public interface IHandler
{
}