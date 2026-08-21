using System.Collections.Frozen;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Configurator;

public record AmanhecerTransformerPipelineOptions(
    FrozenDictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> Configuration);