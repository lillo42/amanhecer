using System;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.Middlewares;

/// <summary>
/// Terminal middleware that resolves the configured handler and invokes it, ending the pipeline.
/// For query handlers, the handler result is stored in <see cref="IPipelineContext.Response"/>.
/// </summary>
/// <param name="factory">The factory used to create the handler instance.</param>
public class ExecuteHandlerMiddleware(IHandlerFactory factory) : IMiddleware
{
    private Type? _handlerType;

    /// <summary>
    /// Initialises the middleware with the handler type to execute.
    /// </summary>
    /// <param name="metadata">The handler <see cref="Type"/> to execute.</param>
    /// <exception cref="NullReferenceException">Thrown when <paramref name="metadata"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="metadata"/> is not a <see cref="Type"/>.</exception>
    public void Initialize(object? metadata)
    {
        _handlerType = metadata switch
        {
            null => throw new NullReferenceException(),
            Type handlerType => handlerType,
            _ => throw new NotSupportedException($"The type {metadata.GetType()} is not supported.")
        };
    }

    /// <summary>
    /// Creates the handler and executes it; the pipeline ends here and <paramref name="next"/> is not invoked.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline; not invoked by this terminal middleware.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the handler has finished.</returns>
    /// <exception cref="NullReferenceException">Thrown when the middleware was not initialised with a handler type.</exception>
    /// <exception cref="NotSupportedException">Thrown when the resolved handler implements neither <see cref="IRequestHandler"/> nor <see cref="IQueryHandler"/>.</exception>
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