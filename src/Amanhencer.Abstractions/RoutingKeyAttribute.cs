using System;

namespace Amanhencer.Abstractions;

/// <summary>
/// Specifies the routing key used to resolve the pipeline for the annotated request or query type.
/// </summary>
/// <param name="key">The routing key associated with the annotated type.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class RoutingKeyAttribute(string key) : Attribute
{
    /// <summary>
    /// Gets the routing key associated with the annotated type.
    /// </summary>
    public string RoutingKey { get; } = key;
}