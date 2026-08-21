using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The registration of a transformer in a pipeline: the implementation type, its position
/// in the pipeline and the metadata passed to it on initialisation.
/// </summary>
/// <param name="TransformerType">The transformer implementation type.</param>
/// <param name="Order">The position of the transformer in the pipeline; lower values run first.</param>
/// <param name="Metadata">Optional metadata passed to the transformer on initialisation.</param>
public record AmanhecerTransformerOptions(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type TransformerType,
    int Order,
    object? Metadata);
