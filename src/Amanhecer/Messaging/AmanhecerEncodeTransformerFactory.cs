using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IEncodeTransformerFactory"/> that resolves encode transformers from the
/// application's service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve transformer instances.</param>
public class AmanhecerEncodeTransformerFactory(IServiceProvider provider) : IEncodeTransformerFactory
{
    /// <inheritdoc />
    public IEncodeTransformer Create(Type transformerType, object? metadata)
    {
        var transformer = (IEncodeTransformer)provider.GetRequiredService(transformerType);
        transformer.Initialize(metadata);
        return transformer;
    }
}
