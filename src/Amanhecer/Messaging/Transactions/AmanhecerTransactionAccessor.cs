using System.Threading;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging;

/// <summary>
/// 
/// </summary>
public class AmanhecerTransactionAccessor : ITransactionAccessor
{
    private readonly AsyncLocal<ITransaction?> _asyncLocal = new();

    /// <summary>
    /// 
    /// </summary>
    public ITransaction? Transaction
    {
        get => _asyncLocal.Value;
        set => _asyncLocal.Value = value;
    }
}