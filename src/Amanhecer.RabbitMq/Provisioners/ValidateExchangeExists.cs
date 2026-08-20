using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

public class ValidateExchangeExists : IExchangeProvisioner
{
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