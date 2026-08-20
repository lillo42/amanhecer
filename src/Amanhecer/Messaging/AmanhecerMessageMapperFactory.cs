using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

public class AmanhecerMessageMapperFactory(IServiceProvider provider) : IMessageMapperFactory
{
    public IMessageMapper Create(Type messageMapperType)
    {
        return (IMessageMapper)provider.GetRequiredService(messageMapperType);
    }
}