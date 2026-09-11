using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions.Metadatas;

/// <summary>
/// The middleware metadata that carries the handler type to execute when the terminal
/// handler-executing middleware is registered in a pipeline.
/// </summary>
/// <param name="HandlerType">The handler <see cref="Type"/> to execute.</param>
public record HandleTypeMetadata(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
    Type HandlerType);
