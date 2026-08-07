using System;

namespace Amanhencer.Abstractions;

public interface IHandlerFactory
{
    IHandler Create(Type handlerType, IPipelineContext context);
}