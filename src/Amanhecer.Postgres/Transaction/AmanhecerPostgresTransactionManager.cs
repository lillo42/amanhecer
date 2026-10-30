using System.Data.Common;
using System.Threading.Tasks;
using Amanhecer.Messaging.Transactions;
using Npgsql;

namespace Amanhecer.Postgres.Transaction;

/// <summary>
/// An <see cref="AmanhecerRdsTransactionManager"/> backed by PostgreSQL: opens
/// <see cref="NpgsqlConnection"/> connections and begins database transactions on them.
/// </summary>
/// <param name="connectionString">The connection string used to open the connection.</param>
/// <param name="createTransaction">Whether to begin a database transaction on the opened connection.</param>
public class AmanhecerPostgresTransactionManager(string connectionString, bool createTransaction)
    : AmanhecerRdsTransactionManager(connectionString, createTransaction)
{
    /// <summary>
    /// Opens a PostgreSQL connection for the given connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to connect with.</param>
    /// <returns>The opened database connection.</returns>
    protected override async Task<DbConnection> CreateConnectionAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}