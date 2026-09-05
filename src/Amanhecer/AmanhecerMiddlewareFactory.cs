using System;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer;

/// <summary>
/// Creates middleware instances by resolving them from the dependency injection container.
/// </summary>
/// <param name="provider">The service provider used to resolve middleware instances.</param>
public class AmanhecerMiddlewareFactory(IServiceProvider provider) : IMiddlewareFactory
{
    /// <summary>
    /// Resolves an instance of the requested middleware type and stores the given metadata in the
    /// context, keyed by the metadata's runtime type.
    /// </summary>
    /// <param name="middlewareType">The type of the middleware to create.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the middleware is created.</param>
    /// <param name="context">The pipeline context the metadata is stored in.</param>
    /// <returns>The resolved middleware instance.</returns>
    public IMiddleware Create(Type middlewareType, object? metadata, AmanhecerContext context)
    {
        context.SetMetadata(metadata);

        var obj = (IMiddleware)provider.GetRequiredService(middlewareType);
        return obj;
    }
}