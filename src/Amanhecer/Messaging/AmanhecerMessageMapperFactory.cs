using System;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IMessageMapperFactory"/> that resolves message mappers from the application's
/// service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve message mapper instances.</param>
public class AmanhecerMessageMapperFactory(IServiceProvider provider) : IMessageMapperFactory
{
    /// <inheritdoc />
    public IMessageMapper Create(Type messageMapperType)
    {
        return (IMessageMapper)provider.GetRequiredService(messageMapperType);
    }
}