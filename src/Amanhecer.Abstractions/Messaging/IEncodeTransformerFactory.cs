using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="IEncodeTransformer"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface IEncodeTransformerFactory
{
    /// <summary>
    /// Creates an instance of the requested transformer type and initialises it with the
    /// supplied metadata.
    /// </summary>
    /// <param name="transformerType">The concrete <see cref="IEncodeTransformer"/>
    /// implementation to create.</param>
    /// <param name="metadata">Optional metadata used to configure the transformer instance.</param>
    /// <returns>The created encode transformer.</returns>
    IEncodeTransformer Create(Type transformerType, object? metadata, AmanhecerContext context);
}
