using System.Threading.Tasks;

namespace Amanhecer.Abstractions;

/// <summary>
/// Represents a pipeline: an ordered chain of middleware that processes a request.
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// Executes the pipeline for the supplied context.
    /// </summary>
    /// <param name="context">The context of the request being processed.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished executing.</returns>
    ValueTask ExecuteAsync(IPipelineContext context);
}