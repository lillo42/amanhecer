using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Transactions;

public class NoTransactionManager : ITransactionManager
{
    public ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
       return new ValueTask<ITransaction>(new NoTransaction());
    }
}