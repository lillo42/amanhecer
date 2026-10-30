using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

/// <summary>
/// A no-op transaction that performs nothing on commit or rollback.
/// </summary>
public class AmanhecerNoTransaction : ITransaction
{
    /// <summary>
    /// Does nothing; there is no underlying transaction to commit.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }

    /// <summary>
    /// Does nothing; there is no underlying transaction to roll back.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }
}