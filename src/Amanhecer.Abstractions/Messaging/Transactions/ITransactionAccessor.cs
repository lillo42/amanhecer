namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// 
/// </summary>
public interface ITransactionAccessor
{
    /// <summary>
    /// 
    /// </summary>
    ITransaction? Transaction { get; set; }
}