using System;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer;

public class AmanhencerHandlerFactory(IServiceProvider serviceProvider) : IHandlerFactory
{
    public IHandler Create(Type handlerType, IPipelineContext context)
    {
        return (IHandler)serviceProvider.GetRequiredService(handlerType);
    }
}