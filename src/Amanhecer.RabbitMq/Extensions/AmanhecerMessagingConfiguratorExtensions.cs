using System;
using Amanhecer.RabbitMq.Configurations;

namespace Amanhecer.Configurator;

public static class AmanhecerMessagingConfiguratorExtensions
{
    public static AmanhecerMessagingConfigurator UsingRabbitMQ(this AmanhecerMessagingConfigurator configurator,
        Action<RabbitMqConfigurator> configure)
    {
        var cfg = new RabbitMqConfigurator();
        configure.Invoke(cfg);

        configurator.AddGateway(cfg.CreateGateway());

        return configurator;
    }
}