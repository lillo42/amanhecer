using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions.Messaging;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public abstract class TransformerAttribute(int order) : Attribute
{
    public int Order { get; set; } = order;

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract Type GetTransformerType();
}

public abstract class TransformeAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    TTransformer>(int order)
    : TransformerAttribute(order)
    where TTransformer : ITransformer
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetTransformerType()
    {
        return typeof(TTransformer);
    }
}