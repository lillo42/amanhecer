using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="IEncodeTransformer"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface IEncodeTransformerFactory
{
    /// <summary>
    /// Creates an instance of the requested transformer type and stores the supplied metadata
    /// in the pipeline context's <see cref="AmanhecerContext.Metadata"/>.
    /// </summary>
    /// <param name="transformerType">The concrete <see cref="IEncodeTransformer"/>
    /// implementation to create.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context for the
    /// transformer to read.</param>
    /// <param name="context">The pipeline context the metadata is stored in.</param>
    /// <returns>The created encode transformer.</returns>
    IEncodeTransformer Create(Type transformerType, object? metadata, AmanhecerContext context);
}
