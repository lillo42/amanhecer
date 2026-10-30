using System.Threading;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging;

/// <summary>
/// Default <see cref="ITransactionAccessor"/> backed by an <see cref="AsyncLocal{T}"/> field,
/// so the ambient transaction follows the current asynchronous flow.
/// </summary>
public class AmanhecerTransactionAccessor : ITransactionAccessor
{
    private readonly AsyncLocal<ITransaction?> _asyncLocal = new();

    /// <summary>
    /// Gets or sets the ambient transaction for the current asynchronous flow, if any.
    /// </summary>
    public ITransaction? Transaction
    {
        get => _asyncLocal.Value;
        set => _asyncLocal.Value = value;
    }
}