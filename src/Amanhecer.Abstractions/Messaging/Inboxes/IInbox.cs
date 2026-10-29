using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Abstractions.Messaging.Inboxes;

/// <summary>
/// 
/// </summary>
public interface IInbox
{
    /// <summary>
    /// 
    /// </summary>
    ITransactionManager TransactionManager { get; }
    
    /// <summary>
    /// 
    /// </summary>
    bool AutoCommit { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="transaction"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction,
        CancellationToken cancellationToken = default);
}