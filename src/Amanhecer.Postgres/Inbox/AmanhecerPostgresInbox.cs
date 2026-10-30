using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Inboxes;
using Amanhecer.Abstractions.Messaging.Transactions;
using Amanhecer.Messaging;
using Amanhecer.Messaging.Inboxes;
using Amanhecer.Messaging.Serializer;
using Amanhecer.Postgres.Transaction;
using Npgsql;
using NpgsqlTypes;

namespace Amanhecer.Postgres.Inbox;

/// <summary>
/// An <see cref="AmanhecerRdsInbox"/> backed by PostgreSQL. Inserts the consumed message
/// id (and, in <see cref="InboxMode.Full"/>, the serialized message payload as JSONB) into
/// a PostgreSQL table, relying on the unique constraint for deduplication.
/// </summary>
/// <param name="configuration">The configuration of the inbox, including the connection
/// string, schema and table.</param>
public partial class AmanhecerPostgresInbox(AmanhecerRdsInboxConfiguration configuration)
    : AmanhecerRdsInbox(configuration)
{
    /// <summary>
    /// Gets or sets the transaction manager that opens PostgreSQL connections.
    /// </summary>
    public override ITransactionManager TransactionManager { get; set; } =
        new AmanhecerPostgresTransactionManager(configuration.ConnectionString, configuration.CreateTransaction);

    /// <summary>
    /// Creates the PostgreSQL insert command for the consumed message: a bare message id
    /// insert in <see cref="InboxMode.Simple"/>, or one carrying the message payload as
    /// JSONB in <see cref="InboxMode.Full"/>.
    /// </summary>
    /// <param name="connection">The connection the command is executed on.</param>
    /// <param name="message">The consumed message being recorded.</param>
    /// <returns>The command that inserts the inbox entry.</returns>
    protected override DbCommand CreateCommand(DbConnection connection, Message message)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            throw new InvalidOperationException();
        }

        var subscription = (ISubscription)message.Metadata[MetadataName.Subscription]!;

        var command = npgsqlConnection.CreateCommand();
        command.Parameters.Add(new NpgsqlParameter { ParameterName = "@MessageId", Value = message.Id });
        command.Parameters.Add(new NpgsqlParameter { ParameterName = "@SubscriptionName", Value = subscription.Name });
        command.Parameters.Add(new NpgsqlParameter { ParameterName = "@At", Value = DateTimeOffset.UtcNow });

        if (Configuration.Mode == InboxMode.Simple)
        {
            command.CommandText = string.Format(SimpleInsert, GetSchema(), GetTable());
        }
        else
        {
            command.CommandText = string.Format(FullInsert, GetSchema(), GetTable());
            command.Parameters.Add(new NpgsqlParameter
            {
                ParameterName = "@Payload",
                Value = JsonSerializer.SerializeToUtf8Bytes(
                    new StoreMessage(message),
                    AmanhecerSerializerContext.Default.StoreMessage),
                NpgsqlDbType = NpgsqlDbType.Jsonb
            });
        }

        return command;
    }

    /// <summary>
    /// Determines whether the exception thrown by the insert represents a duplicate inbox
    /// entry (a unique-constraint violation on the message id and subscription name).
    /// </summary>
    /// <param name="ex">The exception thrown while inserting the inbox entry.</param>
    /// <returns><c>true</c> when the exception means the message was already processed.</returns>
    protected override bool IsDuplicated(Exception ex)
    {
        throw new NotImplementedException();
    }

    private const string SimpleInsert =
        "INSERT INTO \"{0}\".\"{1}\"(\"message_id\", \"subscription_name\", \"at\") VALUES (@MessageId, @SubscriptionName, @At);";

    private const string FullInsert =
        "INSERT INTO \"{0}\".\"{1}\"(\"message_id\", \"subscription_name\", \"at\", \"payload\") VALUES (@MessageId, @SubscriptionName, @At, @Payload);";

    private string GetSchema() => Configuration.Schema ?? "public";

    private string GetTable() => Configuration.Table ?? "amanhecer_inbox";
}