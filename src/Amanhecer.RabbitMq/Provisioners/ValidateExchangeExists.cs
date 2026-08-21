using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="IExchangeProvisioner"/> that validates the exchange exists on the broker,
/// failing if it does not.
/// </summary>
public class ValidateExchangeExists : IExchangeProvisioner
{
    /// <inheritdoc />
#if NETFRAMEWORK
    public async ValueTask ExecuteAsync(IModel channel, Exchange exchange)
    {
        channel.ExchangeDeclarePassive(exchange.Name);
    }
#else
    public async ValueTask ExecuteAsync(IChannel channel, Exchange exchange)
    {
        await channel.ExchangeDeclarePassiveAsync(exchange.Name);
    }
#endif
}