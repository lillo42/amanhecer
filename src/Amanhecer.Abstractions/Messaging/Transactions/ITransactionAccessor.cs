namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// Provides access to the ambient transaction flowing through the current message
/// pipeline, so components can join the active transaction instead of creating their own.
/// </summary>
public interface ITransactionAccessor
{
    /// <summary>
    /// Gets or sets the ambient transaction for the current asynchronous flow, if any.
    /// </summary>
    ITransaction? Transaction { get; set; }
}