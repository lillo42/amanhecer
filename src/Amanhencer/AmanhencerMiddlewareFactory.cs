using System;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer;

public class AmanhencerMiddlewareFactory(IServiceProvider provider) : IMiddlewareFactory
{
    public IMiddleware Create(Type middlewareType, object? metadata)
    {
        var obj = (IMiddleware)provider.GetRequiredService(middlewareType);
        obj.Initialize(metadata);
        return obj;
    }
}