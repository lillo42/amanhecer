using System;

namespace Amanhecer.Abstractions.Messaging;

public interface ITransformerFactory
{
    ITransformer Create(Type transformerType, object? metadata);
}