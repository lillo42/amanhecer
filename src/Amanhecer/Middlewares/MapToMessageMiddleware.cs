using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.Middlewares;

public class MapToMessageMiddleware : IMiddleware
{
    public void Initialize(object? metadata)
    {
        throw new NotImplementedException();
    }

    public ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        throw new NotImplementedException();
    }
}