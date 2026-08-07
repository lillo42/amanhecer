using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Amanhencer.Configurator;

public record AmanhencerPipelineOptions(FrozenDictionary<string, ImmutableList<ImmutableList<AmanhencerMiddlewareOptions>>> Configuration);