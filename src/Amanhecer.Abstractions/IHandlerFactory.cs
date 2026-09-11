using System;

namespace Amanhecer.Abstractions;

/// <summary>
/// Creates handler instances for the pipeline, typically resolving them from a dependency
/// injection container.
/// </summary>
public interface IHandlerFactory
{
    /// <summary>
    /// Creates an instance of the specified handler type.
    /// </summary>
    /// <param name="handlerType">The type of the handler to create.</param>
    /// <param name="context">The context of the pipeline the handler will run in.</param>
    /// <returns>The created handler instance.</returns>
    IHandler Create(Type handlerType, AmanhecerContext context);
}