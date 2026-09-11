using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="IExchangeProvisioner"/> that assumes the exchange already exists and performs no action.
/// </summary>
public class AssumeExchangeExists : IExchangeProvisioner
{
    /// <inheritdoc />
#if NETFRAMEWORK
    public ValueTask ExecuteAsync(IModel channel, Exchange exchange)
    {
        return new ValueTask();
    }
#else
    public ValueTask ExecuteAsync(IChannel channel, Exchange exchange)
    {
        return new ValueTask();
    }
#endif

}