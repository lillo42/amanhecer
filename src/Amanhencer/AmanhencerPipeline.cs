using System.Collections.Immutable;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer;

public class AmanhencerPipeline(ImmutableList<IMiddleware> middlewares) : IPipeline
{
    public async ValueTask ExecuteAsync(IPipelineContext context) => await ExecuteAsync(context, 0);

    private async ValueTask ExecuteAsync(IPipelineContext context, int position)
    {
        if (context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (position == middlewares.Count)
        {
            return;
        }

        await middlewares[position].ExecuteAsync(context, c => ExecuteAsync(c, position + 1));
    }
}