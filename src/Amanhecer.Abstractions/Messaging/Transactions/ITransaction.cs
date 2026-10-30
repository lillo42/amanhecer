using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// A unit of work that groups inbox entries and handler side effects so they commit or
/// roll back together.
/// </summary>
public interface ITransaction
{
    /// <summary>
    /// Commits the transaction, making all its effects permanent.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the commit has run.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction, discarding all its effects.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the rollback has run.</returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}