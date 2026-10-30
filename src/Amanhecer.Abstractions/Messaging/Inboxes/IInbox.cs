using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Abstractions.Messaging.Inboxes;

/// <summary>
/// An inbox: a store used to record the identifiers of consumed messages, providing
/// idempotent (at-most-once) processing by detecting messages that were already handled.
/// </summary>
public interface IInbox
{
    /// <summary>
    /// Gets the transaction manager used to begin a transaction when none is ambient,
    /// so the inbox entry and the message handling share the same commit.
    /// </summary>
    ITransactionManager TransactionManager { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the transaction should be committed by the
    /// inbox pipeline even when it was created by an outer scope.
    /// </summary>
    bool AutoCommit { get; set; }

    /// <summary>
    /// Records the message in the inbox within the given transaction.
    /// </summary>
    /// <param name="message">The consumed message being recorded.</param>
    /// <param name="transaction">The transaction the entry is stored under.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see cref="InboxStoreResponse.Inserted"/> when the message was not seen
    /// before; otherwise <see cref="InboxStoreResponse.AlreadyProcess"/>.</returns>
    ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction,
        CancellationToken cancellationToken = default);
}