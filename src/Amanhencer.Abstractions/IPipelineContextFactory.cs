using System.Threading;

namespace Amanhencer.Abstractions;

/// <summary>
/// Creates the <see cref="IPipelineContext"/> that carries a request through its pipeline.
/// </summary>
public interface IPipelineContextFactory
{
    /// <summary>
    /// Creates a pipeline context for the specified request.
    /// </summary>
    /// <param name="request">The request being dispatched.</param>
    /// <param name="context">The caller-supplied context for the dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The pipeline context for the request.</returns>
    IPipelineContext Create(object request, IContext context, CancellationToken cancellationToken);
}