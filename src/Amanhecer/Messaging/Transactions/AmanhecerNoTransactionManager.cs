using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

/// <summary>
/// A transaction manager that begins <see cref="AmanhecerNoTransaction"/> instances, which perform
/// no actual work.
/// </summary>
public class AmanhecerNoTransactionManager : ITransactionManager
{
    /// <summary>
    /// Begins a no-op transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly started <see cref="AmanhecerNoTransaction"/>.</returns>
    public ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
       return new ValueTask<ITransaction>(new AmanhecerNoTransaction());
    }
}