using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

/// <summary>
/// Base class for an <see cref="ITransactionManager"/> backed by a relational database
/// (RDS). Opens a connection from a connection string and begins a database transaction
/// on it.
/// </summary>
/// <param name="connectionString">The connection string used to open the connection.</param>
/// <param name="createTransaction">Whether to begin a database transaction on the opened connection.</param>
public abstract class AmanhecerRdsTransactionManager(string connectionString, bool createTransaction)
    : ITransactionManager
{
    /// <summary>
    /// Opens a connection and begins a database transaction on it.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly started <see cref="AmanhecerRdsTransaction"/>.</returns>
    public async ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        var connection = await CreateConnectionAsync(connectionString);

        DbTransaction? transaction = null;

        if (createTransaction)
        {
#if NETSTANDARD || NETFRAMEWORK
            transaction = connection.BeginTransaction();
#else
            transaction = await connection.BeginTransactionAsync(cancellationToken);
#endif
        }

        return new AmanhecerRdsTransaction(connection, transaction);
    }

    /// <summary>
    /// Creates the database connection for the given connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to connect with.</param>
    /// <returns>The opened database connection.</returns>
    protected abstract Task<DbConnection> CreateConnectionAsync(string connectionString);
}