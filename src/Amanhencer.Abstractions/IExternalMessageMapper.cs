using System.Threading;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IExternalMessageMapper<TRequest>
{
    ValueTask<ExternalMessage> ToMessageAsync(
        TRequest request,
        CancellationToken cancellationToken = default
    );

    ValueTask<TRequest> ToRequestAsync(
        ExternalMessage message,
        CancellationToken cancellationToken = default
    );
}
