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
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="AmanhecerContext.Metadata"/> when the middleware is created.</param>
    /// <param name="context">The pipeline context the metadata is stored in.</param>
    /// <returns>The created middleware instance.</returns>
    IMiddleware Create(Type middlewareType, object? metadata, AmanhecerContext context);
}