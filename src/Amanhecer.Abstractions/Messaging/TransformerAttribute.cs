using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Declares that a transformer applies to the messages mapped by the message mapper type
/// the attribute is placed on, adding it to the transformer pipeline run for the
/// corresponding messages.
/// </summary>
/// <param name="order">The position of the transformer in the pipeline.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public abstract class TransformerAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets or sets the position of the transformer in the pipeline.
    /// </summary>
    public int Order { get; set; } = order;

    /// <summary>
    /// Gets the type of the <see cref="ITransformer"/> this attribute adds to the pipeline.
    /// </summary>
    /// <returns>The transformer type.</returns>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract Type GetTransformerType();
}

/// <summary>
/// Declares that the transformer <typeparamref name="TTransformer"/> applies to the messages
/// mapped by the message mapper type the attribute is placed on.
/// </summary>
/// <typeparam name="TTransformer">The type of the transformer this attribute adds to the
/// pipeline. Must implement <see cref="IEncodeTransformer"/>, <see cref="IDecodeTransformer"/>
/// or both.</typeparam>
/// <param name="order">The position of the transformer in the pipeline.</param>
public abstract class TransformerAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    TTransformer>(int order)
    : TransformerAttribute(order)
{
    /// <inheritdoc cref="TransformerAttribute.GetTransformerType"/>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetTransformerType()
    {
        return typeof(TTransformer);
    }
}