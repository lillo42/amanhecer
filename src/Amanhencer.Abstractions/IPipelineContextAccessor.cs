namespace Amanhencer.Abstractions;

public interface IPipelineContextAccessor
{
    IPipelineContext? PipelineContext { get; }
}