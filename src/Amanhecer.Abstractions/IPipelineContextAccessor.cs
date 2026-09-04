namespace Amanhecer.Abstractions;

/// <summary>
/// Provides access to the <see cref="AmanhecerContext"/> of the request currently being
/// processed on the calling execution flow.
/// </summary>
public interface IPipelineContextAccessor
{
    /// <summary>
    /// Gets the <see cref="AmanhecerContext"/> of the request being processed, or
    /// <see langword="null"/> when no request is being processed on the current execution flow.
    /// </summary>
    AmanhecerContext? PipelineContext { get; }
}
