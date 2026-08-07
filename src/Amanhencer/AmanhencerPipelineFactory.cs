using System.Collections.Immutable;
using System.Linq;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;

namespace Amanhencer;

/// <summary>
/// Builds the pipelines for a routing key from the configured <see cref="AmanhencerPipelineOptions"/>,
/// instantiating each middleware through an <see cref="IMiddlewareFactory"/>.
/// </summary>
/// <param name="options">The pipeline configuration, keyed by routing key.</param>
/// <param name="middlewareFactory">The factory used to create the middleware instances.</param>
public class AmanhencerPipelineFactory(AmanhencerPipelineOptions options,
    IMiddlewareFactory middlewareFactory) : IPipelineFactory
{
    /// <summary>
    /// Creates the pipelines configured for the routing key of the given context.
    /// </summary>
    /// <param name="context">The pipeline context whose routing key is used to look up the pipelines.</param>
    /// <returns>The pipelines configured for the routing key, or an empty list when none is configured.</returns>
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