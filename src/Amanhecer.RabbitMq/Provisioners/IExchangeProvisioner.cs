using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Provisioners;

/// <summary>
/// Provisions (or validates) the RabbitMQ exchange used by a publication or subscription.
/// </summary>
public interface IExchangeProvisioner
{
    /// <summary>
    /// Provisions the supplied exchange on the given channel.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel on which to provision the exchange.</param>
    /// <param name="exchange">The <see cref="Exchange"/> to provision.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when provisioning has finished.</returns>
    ValueTask ExecuteAsync(
#if NETFRAMEWORK
        IModel channel,
#else
        IChannel channel,
#endif
        Exchange exchange);
}