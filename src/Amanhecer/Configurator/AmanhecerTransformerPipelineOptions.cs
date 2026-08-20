using System.Collections.Frozen;
using System.Collections.Generic;

namespace Amanhecer.Messaging;

public record AmanhecerTransformerPipelineOptions(
    FrozenDictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> Configuration);