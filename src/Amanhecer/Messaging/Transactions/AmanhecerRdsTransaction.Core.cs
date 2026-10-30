#if NETCOREAPP

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Messaging.Transactions;

public partial class AmanhecerRdsTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the underlying database transaction when one exists.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the commit has run.</returns>
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Rolls back the underlying database transaction when one exists.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the rollback has run.</returns>
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Disposes the underlying transaction and connection asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when disposal has run.</returns>
    public async ValueTask DisposeAsync()
    {
        if (transaction != null)
        {
            await transaction.DisposeAsync();
        }

        await connection.DisposeAsync();
    }
}
#endif