using System;

namespace Amanhencer.Configurator;

public record AmanhencerMiddlewareOptions(
    Type MiddlewareType,
    int Order,
    object? Metadata);