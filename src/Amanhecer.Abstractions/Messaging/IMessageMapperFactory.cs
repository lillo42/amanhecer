using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates instances of <see cref="IMessageMapper"/> implementations, resolving their
/// dependencies from the application's service provider.
/// </summary>
public interface IMessageMapperFactory
{
    /// <summary>
    /// Creates an instance of the requested message mapper type.
    /// </summary>
    /// <param name="messageMapperType">The concrete <see cref="IMessageMapper"/>
    /// implementation to create.</param>
    /// <returns>The created message mapper.</returns>
    IMessageMapper Create(Type messageMapperType);
}