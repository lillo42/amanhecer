using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="ITransformer"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface ITransformerFactory
{
    /// <summary>
    /// Creates an instance of the requested transformer type.
    /// </summary>
    /// <param name="transformerType">The concrete <see cref="ITransformer"/> implementation
    /// to create.</param>
    /// <param name="metadata">Optional metadata used to configure the transformer instance.</param>
    /// <returns>The created transformer.</returns>
    ITransformer Create(Type transformerType, object? metadata);
}