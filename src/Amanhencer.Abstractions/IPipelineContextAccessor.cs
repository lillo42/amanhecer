namespace Amanhencer.Abstractions;

/// <summary>
/// Provides access to the <see cref="IPipelineContext"/> of the request currently being
/// processed on the calling execution flow.
/// </summary>
public interface IPipelineContextAccessor
{
    /// <summary>
    /// Gets the <see cref="IPipelineContext"/> of the request being processed, or
    /// <see langword="null"/> when no request is being processed on the current execution flow.
    /// </summary>
    IPipelineContext? PipelineContext { get; }
}
