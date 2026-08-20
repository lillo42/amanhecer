using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

public class CreateIfNotExchange : IExchangeProvisioner
{
    public string Type { get; set; }
    public bool Durable { get; set; }
    public bool AutoDelete { get; set; }
    public IDictionary<string, object>? Arguments { get; set; }

#if NETFRAMEWORK
    public async ValueTask ExecuteAsync(IModel channel, Exchange exchange)
    {
        channel.ExchangeDeclare(exchange.Name, Type, Durable, AutoDelete, Arguments);
    }
#else
    public async ValueTask ExecuteAsync(IChannel channel, Exchange exchange)
    {
        await channel.ExchangeDeclareAsync(exchange.Name, Type, Durable, AutoDelete, Arguments);
    }
#endif
}