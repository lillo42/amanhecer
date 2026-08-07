using System;

namespace Amanhencer.Configurator;

/// <summary>
/// Describes a middleware registration within a pipeline.
/// </summary>
/// <param name="MiddlewareType">The middleware type.</param>
/// <param name="Order">The execution order within the pipeline; lower values run first.</param>
/// <param name="Metadata">Optional metadata passed to the middleware on initialisation.</param>
public record AmanhencerMiddlewareOptions(
    Type MiddlewareType,
    int Order,
    object? Metadata);