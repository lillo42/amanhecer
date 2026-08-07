using System;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer;

/// <summary>
/// Creates middleware instances by resolving them from the dependency injection container.
/// </summary>
/// <param name="provider">The service provider used to resolve middleware instances.</param>
public class AmanhencerMiddlewareFactory(IServiceProvider provider) : IMiddlewareFactory
{
    /// <summary>
    /// Resolves an instance of the requested middleware type and initialises it with the given metadata.
    /// </summary>
    /// <param name="middlewareType">The type of the middleware to create.</param>
    /// <param name="metadata">Optional metadata passed to the middleware on initialisation.</param>
    /// <returns>The resolved and initialised middleware instance.</returns>
    public IMiddleware Create(Type middlewareType, object? metadata)
    {
        var obj = (IMiddleware)provider.GetRequiredService(middlewareType);
        obj.Initialize(metadata);
        return obj;
    }
}