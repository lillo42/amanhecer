using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhencer.Abstractions;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public abstract class MiddlewareAttribute(int order) : Attribute
{
    public int Order { get; set; } = order;
    
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract Type GetMiddlewareType();
}