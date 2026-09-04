using System.Collections.Generic;
using Amanhecer.Abstractions.Options;

namespace Amanhecer.Configurator;

/// <summary>
/// Describes the pipeline configuration for a single routing key.
/// </summary>
/// <param name="RoutingKey">The routing key the pipeline handles.</param>
/// <param name="Middlewares">The middlewares of the pipeline, in execution order.</param>
public record AmanhecerRoutingOptions(
    string RoutingKey,
    IEnumerable<AmanhecerMiddlewareOptions> Middlewares);