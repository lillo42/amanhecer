using System.Collections.Immutable;

namespace Amanhencer.Abstractions;

public interface IPipelineFactory
{
    ImmutableList<IPipeline> Create(IPipelineContext context);
}