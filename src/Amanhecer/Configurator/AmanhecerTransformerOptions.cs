using System;

namespace Amanhecer.Messaging;

public record AmanhecerTransformerOptions(
    Type MiddlewareType,
    int Order,
    object? Metadata);