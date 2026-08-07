using System;

namespace Amanhencer.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class RoutingKeyAttribute(string key) : Attribute
{
    public string RoutingKey { get; } = key;
}