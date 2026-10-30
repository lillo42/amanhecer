using System;
using System.Data.Common;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

/// <summary>
/// An <see cref="ITransaction"/> backed by a database transaction over an ADO.NET
/// <see cref="DbConnection"/>, so inbox entries and handler side effects share one commit.
/// </summary>
/// <param name="connection">The connection the transaction runs on.</param>
/// <param name="transaction">The underlying database transaction, if any.</param>
public partial class AmanhecerRdsTransaction(DbConnection connection, DbTransaction? transaction) : ITransaction, IDisposable
{
    /// <summary>
    /// Gets the connection the transaction runs on.
    /// </summary>
    public DbConnection Connection => connection;

    /// <summary>
    /// Gets the underlying database transaction, if any.
    /// </summary>
    public DbTransaction? Transaction => transaction;

    /// <summary>
    /// Disposes the underlying transaction and connection.
    /// </summary>
    public void Dispose()
    {
        transaction?.Dispose();
        connection.Dispose();
    }
}