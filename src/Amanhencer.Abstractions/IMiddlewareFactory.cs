using System;

namespace Amanhencer.Abstractions;

public interface IMiddlewareFactory
{
    IMiddleware Create(Type middlewareType, object? metadata);
}