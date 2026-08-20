using Amanhecer.RabbitMq.Provisioners;

namespace Amanhecer.RabbitMq;

public class Exchange
{
    public string Name { get; set; }
    public IExchangeProvisioner Provisioner { get; set; } = new AssumeExchangeExists();
}