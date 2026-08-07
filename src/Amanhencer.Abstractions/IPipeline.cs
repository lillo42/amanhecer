using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IPipeline
{
    ValueTask ExecuteAsync(IPipelineContext context);
}