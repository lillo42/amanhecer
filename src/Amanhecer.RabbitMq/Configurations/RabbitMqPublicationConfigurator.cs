using System;

namespace Amanhecer.RabbitMq.Configurations;

public class RabbitMqPublicationConfigurator
{
    private string? _routingKey;
    
    private string? _rabbitMqRoutingKey;

    public RabbitMqPublicationConfigurator RabbitMqRoutingKey(string routingKey)
    {
        _rabbitMqRoutingKey = routingKey;
        return this;
    }

    internal RabbitMqPublication ToPublication()
    {
        if(string.IsNullOrEmpty(_routingKey))
        {
            throw new NotImplementedException();
        }
        
        if (string.IsNullOrEmpty(_rabbitMqRoutingKey))
        {
            throw new NotImplementedException();
        }

        return new RabbitMqPublication
        {
            RoutingKey = _routingKey,
            RabbitMqRoutingKey = _rabbitMqRoutingKey
        };
    }
}