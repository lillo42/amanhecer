using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Amanhecer.Configurator;

/// <summary>
/// Holds the immutable pipeline configuration: the middleware lists for each routing key.
/// </summary>
/// <param name="Configuration">Maps each routing key to its configured pipelines; each pipeline is an ordered list of middleware options.</param>
public record AmanhecerPipelineOptions(FrozenDictionary<string, ImmutableList<ImmutableList<AmanhecerMiddlewareOptions>>> Configuration);