using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Inboxes;
using Amanhecer.Abstractions.Messaging.Transactions;
using Amanhecer.Messaging.Transactions;

namespace Amanhecer.Messaging.Inboxes;

/// <summary>
/// Base class for an inbox stored in a relational database (RDS). Inserts the consumed
/// message id through a database command and treats unique-constraint violations as the
/// <see cref="InboxStoreResponse.AlreadyProcess"/> signal, providing at-most-once
/// processing on top of any ADO.NET provider.
/// </summary>
public abstract class AmanhecerRdsInbox(AmanhecerRdsInboxConfiguration configuration) : IInbox
{
    /// <inheritdoc cref="IInbox.TransactionManager"/>
    public abstract ITransactionManager TransactionManager { get; set; }

    /// <inheritdoc cref="IInbox.Configuration"/>
    public AmanhecerRdsInboxConfiguration Configuration { get; } = configuration;

    InboxConfiguration IInbox.Configuration => Configuration;


#if NETSTANDARD || NETFRAMEWORK
    /// <inheritdoc cref="IInbox.StoreAsync"/>
    public ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction,
        CancellationToken cancellationToken
            = default)
    {
        if (transaction is not AmanhecerRdsTransaction rdsTransaction)
        {
            throw new NotImplementedException();
        }

        try
        {
            using var command = CreateCommand(rdsTransaction.Connection, message);
            command.CommandTimeout = Configuration.CommandTimeout;
            command.Transaction = rdsTransaction.Transaction;

            command.ExecuteNonQuery();

            return new ValueTask<InboxStoreResponse>(InboxStoreResponse.Inserted);
        }
        catch (Exception e) when(IsDuplicated(e))
        {
            return new ValueTask<InboxStoreResponse>(InboxStoreResponse.AlreadyProcess);
        }
    }
#else
    /// <inheritdoc cref="IInbox.StoreAsync"/>
    public async ValueTask<InboxStoreResponse> StoreAsync(Message message, ITransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (transaction is not AmanhecerRdsTransaction rdsTransaction)
        {
            throw new NotImplementedException();
        }

        try
        {
            await using var command = CreateCommand(rdsTransaction.Connection, message);
            command.CommandTimeout = Configuration.CommandTimeout;
            command.Transaction = rdsTransaction.Transaction;

            await command.ExecuteNonQueryAsync(cancellationToken);

            return InboxStoreResponse.Inserted;
        }
        catch (Exception e) when (IsDuplicated(e))
        {
            return InboxStoreResponse.AlreadyProcess;
        }
    }
#endif

    /// <summary>
    /// Creates the command that inserts the message id into the inbox table, bound to the
    /// given connection.
    /// </summary>
    /// <param name="connection">The connection the command is executed on.</param>
    /// <param name="message">The consumed message being recorded.</param>
    /// <returns>The command that inserts the inbox entry.</returns>
    protected abstract DbCommand CreateCommand(DbConnection connection, Message message);

    /// <summary>
    /// Determines whether the exception thrown by the insert represents a duplicate inbox
    /// entry (for example a unique-constraint violation on the message id).
    /// </summary>
    /// <param name="ex">The exception thrown while inserting the inbox entry.</param>
    /// <returns><c>true</c> when the exception means the message was already processed.</returns>
    protected abstract bool IsDuplicated(Exception ex);
    
    
}