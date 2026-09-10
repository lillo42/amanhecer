using System.Collections.Generic;
using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Configurator;
using Amanhecer.Extensions;

namespace Amanhecer;

/// <summary>
/// Builds the pipelines for a routing key from the configured <see cref="AmanhecerPipelineOptions"/>,
/// instantiating each middleware through an <see cref="IMiddlewareFactory"/>.
/// </summary>
/// <param name="options">The pipeline configuration, keyed by routing key.</param>
/// <param name="middlewareFactory">The factory used to create the middleware instances.</param>
public class AmanhecerPipelineFactory(
    AmanhecerPipelineOptions options,
    IMiddlewareFactory middlewareFactory) : IPipelineFactory
{
    /// <summary>
    /// Creates the pipelines configured for the routing key of the given context.
    /// </summary>
    /// <param name="context">The pipeline context whose routing key is used to look up the pipelines.</param>
    /// <returns>The pipelines configured for the routing key, or an empty list when none is configured.</returns>
    public IReadOnlyList<IPipeline> Create(AmanhecerContext context)
    {
        if (!options.Configuration.TryGetValue(context.RoutingKey, out var pipelines) || pipelines.Count == 0)
        {
            return [];
        }

        return
        [
            .. pipelines
                .Select(cfg =>
                {
                    // Middlewares are created against a per-pipeline metadata bag so pipelines
                    // sharing the caller's context don't overwrite each other's metadata; the
                    // pipeline merges the bag into the context it executes with.
                    var metadataBag = new AmanhecerContext();

                    var middlewares = cfg;
                    if (context.Middlewares != null)
                    {
                        middlewares = middlewares
                            .AppendRange(context.Middlewares);
                    }

                    return new AmanhecerPipeline([
                        .. middlewares
                            .OrderBy(x => x.Order)
                            .Select(opt => middlewareFactory.Create(opt.MiddlewareType, opt.Metadata, metadataBag))
                    ], metadataBag.Metadata);
                })
        ];
    }
}