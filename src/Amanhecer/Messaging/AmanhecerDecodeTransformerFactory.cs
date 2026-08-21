using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

public class AmanhecerDecodeTransformerFactory(IServiceProvider provider) : IDecodeTransformerFactory
{
    public IDecodeTransformer Create(Type transformerType, object? metadata)
    {
        var transformer = (IDecodeTransformer)provider.GetRequiredService(transformerType);
        transformer.Initialize(metadata);
        return transformer;
    }
}
