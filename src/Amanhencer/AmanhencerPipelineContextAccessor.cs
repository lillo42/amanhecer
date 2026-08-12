using System.Threading;
using Amanhencer.Abstractions;

namespace Amanhencer;

/// <summary>
/// The default <see cref="IPipelineContextAccessor"/> implementation. Stores the current
/// <see cref="IPipelineContext"/> in an <see cref="AsyncLocal{T}"/>, so it flows with the
/// asynchronous execution context.
/// </summary>
public class AmanhencerPipelineContextAccessor : IPipelineContextAccessor
{
    private readonly AsyncLocal<IPipelineContext?> _accessor = new();

    /// <inheritdoc cref="IPipelineContextAccessor.PipelineContext"/>
    public IPipelineContext? PipelineContext
    {
        get => _accessor.Value;
        set => _accessor.Value = value;
    }
}
