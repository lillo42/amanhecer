using System.Collections.Generic;

namespace Amanhencer.Configurator;

public record AmanhencerRoutingOptions(
    string RoutingKey,
    IEnumerable<AmanhencerMiddlewareOptions> MiddlewareOptions);