using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// An <see cref="IExchangeProvisioner"/> that declares the exchange, creating it if it does not exist yet.
/// </summary>
public class CreateIfNotExchange : IExchangeProvisioner
{
    /// <summary>
    /// Gets or sets the exchange type (for example <c>direct</c>, <c>topic</c> or <c>fanout</c>).
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exchange survives a broker restart.
    /// </summary>
    public bool Durable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exchange is deleted when it is no longer in use.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets additional arguments passed to the exchange declaration.
    /// </summary>
    public IDictionary<string, object>? Arguments { get; set; }

    /// <inheritdoc />
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