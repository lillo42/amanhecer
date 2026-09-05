using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IMessagePumperFactory"/> that resolves <see cref="IMessagePumper"/> instances
/// from the application's service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve message pumpers.</param>
public class AmanhecerMessagePumperFactory(IServiceProvider provider) : IMessagePumperFactory
{
    /// <inheritdoc />
    public IMessagePumper Create()
    {
        return provider.GetRequiredService<IMessagePumper>();
    }
}
