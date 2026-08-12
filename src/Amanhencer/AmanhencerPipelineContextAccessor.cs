using System.Threading;
using Amanhencer.Abstractions;

namespace Amanhencer;

public class AmanhencerPipelineContextAccessor : IPipelineContextAccessor
{
    private readonly AsyncLocal<IPipelineContext?> _accessor = new();

    public IPipelineContext? PipelineContext
    {
        get => _accessor.Value;
        set => _accessor.Value = value;
    }
}