using System.Collections.Generic;

namespace Amanhecer.Abstractions;

/// <summary>
/// Creates the pipelines that should process a given pipeline context.
/// </summary>
public interface IPipelineFactory
{
    /// <summary>
    /// Resolves the pipelines matching the routing key of the supplied context.
    /// </summary>
    /// <param name="context">The context of the request being processed.</param>
    /// <returns>The pipelines that match the context, or an empty list when none was found.</returns>
    IReadOnlyList<IPipeline> Create(AmanhecerContext context);
}