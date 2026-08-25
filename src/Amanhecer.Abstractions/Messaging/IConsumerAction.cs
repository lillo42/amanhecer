namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The outcome returned by a consumer handler to instruct the transport how to
/// settle the message, such as <see cref="Ack"/>, <see cref="Nack"/> or <see cref="Defer"/>.
/// </summary>
public interface IConsumerAction;