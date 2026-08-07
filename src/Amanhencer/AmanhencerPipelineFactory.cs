using System.Collections.Immutable;
using System.Linq;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;

namespace Amanhencer;

public class AmanhencerPipelineFactory(AmanhencerPipelineOptions options,
    IMiddlewareFactory middlewareFactory) : IPipelineFactory
{
    public ImmutableList<IPipeline> Create(IPipelineContext context)
    {
        if (options.Configuration.TryGetValue(context.RoutingKey, out var pipelines))
        {
            return
            [
                .. pipelines
                    .Select(middlewares =>
                    {
                        IPipeline pipeline = new AmanhencerPipeline([
                            .. middlewares
                                .Select(middleware => middlewareFactory.Create(middleware.MiddlewareType, middleware.Metadata))
                        ]);
                        return pipeline;
                    })
            ];
        }
        
        return ImmutableList<IPipeline>.Empty;
    }
}