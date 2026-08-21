using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="IDecodeTransformer"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface IDecodeTransformerFactory
{
    /// <summary>
    /// Creates an instance of the requested transformer type and initialises it with the
    /// supplied metadata.
    /// </summary>
    /// <param name="transformerType">The concrete <see cref="IDecodeTransformer"/>
    /// implementation to create.</param>
    /// <param name="metadata">Optional metadata used to configure the transformer instance.</param>
    /// <returns>The created decode transformer.</returns>
    IDecodeTransformer Create(Type transformerType, object? metadata);
}
