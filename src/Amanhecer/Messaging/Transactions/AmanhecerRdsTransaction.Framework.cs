#if NETSTANDARD || NETFRAMEWORK
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Messaging.Transactions;

public partial class AmanhecerRdsTransaction
{
    /// <summary>
    /// Commits the underlying database transaction when one exists.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the commit has run.</returns>
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        transaction?.Commit();
        return new ValueTask();
    }

    /// <summary>
    /// Rolls back the underlying database transaction when one exists.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the rollback has run.</returns>
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        transaction?.Rollback();
        return new ValueTask();
    }
}
#endif