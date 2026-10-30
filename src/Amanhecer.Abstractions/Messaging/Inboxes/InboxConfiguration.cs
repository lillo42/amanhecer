namespace Amanhecer.Abstractions.Messaging.Inboxes;

/// <summary>
/// Configuration of an <see cref="IInbox"/>.
/// </summary>
public class InboxConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the transaction should be committed by the
    /// inbox pipeline even when it was created by an outer scope. Defaults to
    /// <c>true</c>.
    /// </summary>
    public bool AutoCommitTransaction { get; set; } = true;

    /// <summary>
    /// Gets or sets the deduplication mode applied to consumed messages. Defaults to
    /// <see cref="InboxMode.Simple"/>.
    /// </summary>
    public InboxMode Mode { get; set; } = InboxMode.Simple;
}
