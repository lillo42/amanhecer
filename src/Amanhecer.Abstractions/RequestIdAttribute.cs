using System;

namespace Amanhecer.Abstractions;

/// <summary>
/// Marks the property or method of a request that supplies its request identifier.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class RequestIdAttribute : Attribute;