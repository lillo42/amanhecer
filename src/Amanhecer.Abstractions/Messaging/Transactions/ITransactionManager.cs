using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// 
/// </summary>
public interface ITransactionManager
{
    ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}