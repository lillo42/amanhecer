namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// An acknowledgement returned by a consumer handler to signal that the message was
/// processed successfully and can be removed from the transport.
/// </summary>
public record Ack;