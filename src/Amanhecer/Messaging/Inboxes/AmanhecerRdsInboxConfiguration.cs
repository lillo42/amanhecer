using Amanhecer.Abstractions.Messaging.Inboxes;

namespace Amanhecer.Messaging.Inboxes;

/// <summary>
/// Configuration for an <see cref="AmanhecerRdsInbox"/> stored in a relational database.
/// </summary>
/// <param name="connectionString">The connection string used to connect to the database.</param>
public abstract class AmanhecerRdsInboxConfiguration(string connectionString) : InboxConfiguration
{
    /// <summary>
    /// Gets the connection string used to connect to the database.
    /// </summary>
    public string ConnectionString => connectionString;

    /// <summary>
    /// Gets or sets a value indicating whether the inbox creates its own transaction when
    /// none is ambient. Defaults to <c>true</c>.
    /// </summary>
    public bool CreateTransaction { get; set; } = true;

    /// <summary>
    /// Gets or sets the command timeout in seconds applied to the inbox insert.
    /// </summary>
    public int CommandTimeout { get; set; }

    /// <summary>
    /// Gets or sets the schema the inbox table lives in, if any.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Gets or sets the name of the table the inbox entries are stored in.
    /// </summary>
    public string? Table { get; set; }
}