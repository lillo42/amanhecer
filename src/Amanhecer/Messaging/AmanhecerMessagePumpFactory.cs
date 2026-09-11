using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IMessagePumpFactory"/> that resolves <see cref="IMessagePump"/> instances
/// from the application's service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve message pumps.</param>
public class AmanhecerMessagePumpFactory(IServiceProvider provider) : IMessagePumpFactory
{
    /// <inheritdoc />
    public IMessagePump Create()
    {
        return provider.GetRequiredService<IMessagePump>();
    }
}
