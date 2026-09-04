using System;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Represents a middleware component in a pipeline, able to run logic before and after the rest
/// of the pipeline by wrapping the call to the next component.
/// </summary>
public interface IMiddleware
{
    /// <summary>
    /// Executes the middleware. Invoke <paramref name="next"/> to continue executing the pipeline.
    /// </summary>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the middleware has finished.</returns>
    ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next);
}