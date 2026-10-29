using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging.Transactions;

/// <summary>
/// 
/// </summary>
public interface ITransaction
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}