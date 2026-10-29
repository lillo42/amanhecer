using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Inboxes;
using Amanhecer.Abstractions.Messaging.Transactions;
using Amanhecer.Messaging.Transactions;

namespace Amanhecer.Messaging.Inboxes;

/// <summary>
/// 
/// </summary>
public class NoInbox : IInbox
{
    /// <summary>
    /// 
    /// </summary>
    public ITransactionManager TransactionManager { get; } = new NoTransactionManager();
    
    /// <summary>
    /// 
    /// </summary>
    public bool AutoCommit { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="transaction"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction, CancellationToken cancellationToken = default)
    {
        return new ValueTask<InboxStoreResponse>(InboxStoreResponse.Inserted);
    }
}