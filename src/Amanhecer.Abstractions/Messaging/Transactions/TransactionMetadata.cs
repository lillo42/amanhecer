namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// Carries the transaction an outer pipeline scope opened, so inner middlewares can join
/// it instead of beginning a new one.
/// </summary>
/// <param name="Transaction">The ambient transaction to join.</param>
public record TransactionMetadata(ITransaction Transaction);
