using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="IDecodeTransformer"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface IDecodeTransformerFactory
{
    /// <summary>
    /// Creates an instance of the requested transformer type and stores the supplied metadata
    /// in the pipeline context's <see cref="AmanhecerContext.Metadata"/>.
    /// </summary>
    /// <param name="transformerType">The concrete <see cref="IDecodeTransformer"/>
    /// implementation to create.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context for the
    /// transformer to read.</param>
    /// <param name="context">The pipeline context the metadata is stored in.</param>
    /// <returns>The created decode transformer.</returns>
    IDecodeTransformer Create(Type transformerType, object? metadata, AmanhecerContext context);
}
