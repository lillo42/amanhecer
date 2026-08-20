using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

public class AmanhecerTransformerFactory(IServiceProvider provider) : ITransformerFactory
{
    public ITransformer Create(Type transformerType, object? metadata)
    {
        return (ITransformer)provider.GetRequiredService(transformerType);
    }
}