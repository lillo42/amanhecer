using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Inboxes;
using Amanhecer.Abstractions.Messaging.Transactions;
using Amanhecer.Messaging.Transactions;

namespace Amanhecer.Messaging.Inboxes;

/// <summary>
/// A no-op inbox that records nothing and always reports a message as inserted, so the
/// pipeline runs the handler once per delivery without deduplication.
/// </summary>
public class AmanhecerNoInbox : IInbox
{
    /// <summary>
    /// Gets the transaction manager that begins no-op transactions.
    /// </summary>
    public ITransactionManager TransactionManager { get; set; } = new AmanhecerNoTransactionManager();

    /// <summary>
    /// Gets the inbox configuration, left at its defaults since nothing is recorded.
    /// </summary>
    public InboxConfiguration Configuration { get; } = new InboxConfiguration();

    /// <summary>
    /// Gets or sets a value indicating whether transactions should be committed by the
    /// inbox pipeline. Has no effect for this implementation.
    /// </summary>
    public bool AutoCommit { get; set; }

    /// <summary>
    /// Always reports the message as inserted, since nothing is recorded.
    /// </summary>
    /// <param name="message">The consumed message being recorded.</param>
    /// <param name="transaction">The transaction the entry would be stored under.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see cref="InboxStoreResponse.Inserted"/> on every call.</returns>
    public ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction, CancellationToken cancellationToken = default)
    {
        return new ValueTask<InboxStoreResponse>(InboxStoreResponse.Inserted);
    }
}