using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

public class AmanhecerEncodeTransformerFactory(IServiceProvider provider) : IEncodeTransformerFactory
{
    public IEncodeTransformer Create(Type transformerType, object? metadata)
    {
        var transformer = (IEncodeTransformer)provider.GetRequiredService(transformerType);
        transformer.Initialize(metadata);
        return transformer;
    }
}
