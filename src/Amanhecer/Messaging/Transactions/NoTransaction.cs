using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

public class NoTransaction : ITransaction
{
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }

    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }
}