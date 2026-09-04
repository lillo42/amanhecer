using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

public class AmanhecerMessagePumperFactory(IServiceProvider provider) : IMessagePumperFactory
{
    public IMessagePumper Create()
    {
        return provider.GetRequiredService<IMessagePumper>();
    }
}