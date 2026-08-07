using System;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.Middlewares;

public class ExecuteHandlerMiddleware(IHandlerFactory factory) : IMiddleware
{
    private Type? _handlerType;

    public void Initialize(object? metadata)
    {
        _handlerType = metadata switch
        {
            null => throw new NullReferenceException(),
            Type handlerType => handlerType,
            _ => throw new NotSupportedException($"The type {metadata.GetType()} is not supported.")
        };
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        if (_handlerType == null)
        {
            // TODO: add error message
            throw new NullReferenceException();
        }

        var handler = factory.Create(_handlerType, context);
        switch (handler)
        {
            case IRequestHandler requestHandler:
                await requestHandler.HandleAsync(context.Request, context, context.CancellationToken);
                break;
            case IQueryHandler queryHandler:
                context.Response = await queryHandler.HandleAsync(context.Request, context, context.CancellationToken);
                break;
            default:
                throw new NotSupportedException($"The type {context.GetType()} is not supported.");
        }
    }
}