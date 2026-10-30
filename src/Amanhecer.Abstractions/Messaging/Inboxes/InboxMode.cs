namespace Amanhecer.Abstractions.Messaging.Inboxes;

/// <summary>
/// The deduplication mode applied by an <see cref="IInbox"/> to consumed messages.
/// </summary>
public enum InboxMode
{
    /// <summary>
    /// Deduplicates on the message id only, skipping messages that were recorded before.
    /// </summary>
    Simple,

    /// <summary>
    /// Deduplicates on the message id and its content, also skipping messages that were
    /// recorded before with the same payload.
    /// </summary>
    Full
}
