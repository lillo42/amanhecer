using System.Collections.Generic;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Defines the strategy used to execute the pipelines resolved for a request.
/// </summary>
public interface IExecutingStrategy
{
    /// <summary>
    /// Executes the given pipelines for the supplied pipeline context.
    /// </summary>
    /// <param name="context">The context of the request being processed.</param>
    /// <param name="pipelines">The pipelines resolved for the request.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipelines have been executed.</returns>
    ValueTask ExecuteAsync(AmanhecerContext context, IReadOnlyList<IPipeline> pipelines);
}