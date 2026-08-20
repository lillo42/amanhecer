using System;

namespace Amanhecer.Abstractions.Messaging;

public interface IMessageMapperFactory
{
    IMessageMapper Create(Type messageMapperType);
}