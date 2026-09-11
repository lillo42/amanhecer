using System.Collections.Frozen;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Configurator;

/// <summary>
/// The transformer pipelines available to the messaging layer.
/// </summary>
/// <param name="Configuration">The transformer pipelines, keyed by pipeline name.</param>
public record AmanhecerTransformerPipelineOptions(
    FrozenDictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> Configuration);