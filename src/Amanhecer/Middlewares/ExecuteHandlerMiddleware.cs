using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Metadatas;

namespace Amanhecer.Middlewares;

/// <summary>
/// Terminal middleware that resolves the configured handler and invokes it, ending the pipeline.
/// For query handlers, the handler result is stored in <see cref="AmanhecerContext.Response"/>.
/// The handler <see cref="Type"/> is read from the context metadata, where it is stored when the
/// middleware is created (see <see cref="AmanhecerMiddlewareFactory"/>).
/// </summary>
/// <param name="factory">The factory used to create the handler instance.</param>
public class ExecuteHandlerMiddleware(IHandlerFactory factory) : IMiddleware
{
    /// <summary>
    /// Creates the handler and executes it; the pipeline ends here and <paramref name="next"/> is not invoked.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline; not invoked by this terminal middleware.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the handler has finished.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the context carries no handler type metadata.</exception>
    /// <exception cref="NotSupportedException">Thrown when the resolved handler implements neither <see cref="IRequestHandler"/> nor <see cref="IQueryHandler"/>.</exception>
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        var metadata = context.GetMetadata<HandleTypeMetadata>()
            ?? throw new InvalidOperationException(
                $"The middleware '{nameof(ExecuteHandlerMiddleware)}' requires the handler type to be " +
                "stored in the context metadata.");

        var handler = factory.Create(metadata.HandlerType, context);
        switch (handler)
        {
            case IRequestHandler requestHandler:
                await requestHandler.HandleAsync(context.Request, context, context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
                break;
            case IQueryHandler queryHandler:
                context.Response = await queryHandler.HandleAsync(context.Request, context, context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
                break;
            default:
                throw new NotSupportedException($"The type {context.GetType()} is not supported.");
        }
    }
}
