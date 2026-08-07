using System.Threading;

namespace Amanhencer.Abstractions;

public interface IPipelineContextFactory
{
    IPipelineContext Create(object request, IContext context, CancellationToken cancellationToken);
}