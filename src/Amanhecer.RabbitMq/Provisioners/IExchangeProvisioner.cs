using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

public interface IExchangeProvisioner
{
    ValueTask ExecuteAsync(
#if NETFRAMEWORK
        IModel channel,
#else
        IChannel channel,
#endif
        Exchange exchange);
}