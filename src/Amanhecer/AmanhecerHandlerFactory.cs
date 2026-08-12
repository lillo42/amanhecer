using System;
using Amanhecer.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer;

/// <summary>
/// Creates handler instances by resolving them from the dependency injection container.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve handler instances.</param>
public class AmanhecerHandlerFactory(IServiceProvider serviceProvider) : IHandlerFactory
{
    /// <summary>
    /// Resolves an instance of the requested handler type.
    /// </summary>
    /// <param name="handlerType">The type of the handler to create.</param>
    /// <param name="context">The current pipeline context.</param>
    /// <returns>The resolved handler instance.</returns>
    public IHandler Create(Type handlerType, IPipelineContext context)
    {
        return (IHandler)serviceProvider.GetRequiredService(handlerType);
    }
}