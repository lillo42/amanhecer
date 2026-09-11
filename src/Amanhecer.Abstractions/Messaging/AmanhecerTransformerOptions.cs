using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The registration of a transformer in a pipeline: the implementation type, its position
/// in the pipeline and the metadata stored in the pipeline context when it is created.
/// </summary>
/// <param name="TransformerType">The transformer implementation type.</param>
/// <param name="Order">The position of the transformer in the pipeline; lower values run first.</param>
/// <param name="Metadata">Optional metadata stored in the pipeline context's
/// <see cref="AmanhecerContext.Metadata"/> when the transformer is created.</param>
public record AmanhecerTransformerOptions(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type TransformerType,
    int Order,
    object? Metadata);
