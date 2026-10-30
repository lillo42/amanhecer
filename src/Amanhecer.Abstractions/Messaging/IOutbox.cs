namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Marker abstraction for an outbox: a store used to record messages that were published
/// within a transaction, so outgoing effects only become visible when that transaction
/// commits.
/// </summary>
public interface IOutbox
{
}