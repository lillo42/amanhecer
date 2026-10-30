using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// Creates transactions for a backing store, used when no ambient transaction exists yet.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly started transaction.</returns>
    ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}