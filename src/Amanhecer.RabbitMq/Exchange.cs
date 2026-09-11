using Amanhecer.RabbitMq.Provisioners;

namespace Amanhecer.RabbitMq;

/// <summary>
/// Represents a RabbitMQ exchange, together with the provisioner used to set it up.
/// </summary>
public class Exchange
{
    /// <summary>
    /// Gets or sets the name of the exchange.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the provisioner used to set up the exchange. Defaults to
    /// <see cref="AssumeExchangeExists"/>, which assumes the exchange already exists.
    /// </summary>
    public IExchangeProvisioner Provisioner { get; set; } = new AssumeExchangeExists();
}