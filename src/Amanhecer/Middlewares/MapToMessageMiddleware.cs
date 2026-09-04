using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.Middlewares;

/// <summary>
/// Middleware that maps the pipeline context to a message. Not yet implemented.
/// </summary>
public class MapToMessageMiddleware : IMiddleware
{
    /// <inheritdoc />
    public void Initialize(object? metadata)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        throw new NotImplementedException();
    }
}