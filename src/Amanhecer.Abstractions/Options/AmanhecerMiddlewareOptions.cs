using System;

namespace Amanhecer.Abstractions.Options;

/// <summary>
/// Describes a middleware registration within a pipeline.
/// </summary>
/// <param name="MiddlewareType">The middleware type.</param>
/// <param name="Order">The execution order within the pipeline; lower values run first.</param>
/// <param name="Metadata">Optional metadata stored in the pipeline context's
/// <see cref="AmanhecerContext.Metadata"/> when the middleware is created.</param>
public record AmanhecerMiddlewareOptions(
    Type MiddlewareType,
    int Order,
    object? Metadata);