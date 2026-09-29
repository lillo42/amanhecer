using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.Middlewares;

/// <summary>
/// An <see cref="IMiddleware"/> implementation that delegates execution to an
/// inline delegate, allowing anonymous middlewares to be added to a pipeline.
/// </summary>
/// <param name="func">The delegate executed as the middleware body.</param>
public class AnonymousMiddleware(Func<AmanhecerContext, Func<AmanhecerContext, ValueTask>, ValueTask> func): IMiddleware
{
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        await func(context, next).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}