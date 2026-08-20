using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

public class AssumeExchangeExists : IExchangeProvisioner
{
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