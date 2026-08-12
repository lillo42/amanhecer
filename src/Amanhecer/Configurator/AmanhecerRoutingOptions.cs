using System.Collections.Generic;

namespace Amanhecer.Configurator;

/// <summary>
/// Describes the pipeline configuration for a single routing key.
/// </summary>
/// <param name="RoutingKey">The routing key the pipeline handles.</param>
/// <param name="MiddlewareOptions">The middlewares of the pipeline, in execution order.</param>
public record AmanhecerRoutingOptions(
    string RoutingKey,
    IEnumerable<AmanhecerMiddlewareOptions> MiddlewareOptions);