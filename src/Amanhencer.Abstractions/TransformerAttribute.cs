using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhencer.Abstractions;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public abstract class TransformerAttribute(int order) : MiddlewareAttribute(order)
{
   [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
   public abstract Type GetTransformerType();

   
   [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
   public override Type GetMiddlewareType() => GetTransformerType();
}