using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IExecutingStrategy
{
    ValueTask ExecuteAsync(IPipelineContext context, ImmutableList<IPipeline> pipelines);
}