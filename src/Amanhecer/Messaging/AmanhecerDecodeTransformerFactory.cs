using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IDecodeTransformerFactory"/> that resolves decode transformers from the
/// application's service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve transformer instances.</param>
public class AmanhecerDecodeTransformerFactory(IServiceProvider provider) : IDecodeTransformerFactory
{
    /// <inheritdoc />
    public IDecodeTransformer Create(Type transformerType, object? metadata)
    {
        var transformer = (IDecodeTransformer)provider.GetRequiredService(transformerType);
        transformer.Initialize(metadata);
        return transformer;
    }
}
