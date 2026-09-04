using System;

namespace Amanhecer.Abstractions;

/// <summary>
/// Creates middleware instances for the pipeline, typically resolving them from a dependency
/// injection container.
/// </summary>
public interface IMiddlewareFactory
{
    /// <summary>
    /// Creates an instance of the specified middleware type.
    /// </summary>
    /// <param name="middlewareType">The type of the middleware to create.</param>
    /// <param name="metadata">Optional metadata used to initialise the middleware instance.</param>
    /// <returns>The created middleware instance.</returns>
    IMiddleware Create(Type middlewareType, object? metadata, AmanhecerContext context);
}