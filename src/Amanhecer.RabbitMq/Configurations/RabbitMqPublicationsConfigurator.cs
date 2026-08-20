using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Configurations;

public class RabbitMqPublicationsConfigurator
{
    private List<RabbitMqPublication> _publications = [];
    public RabbitMqPublicationsConfigurator AddPublication(Action<RabbitMqPublicationConfigurator> configure)
    {
        var cfg = new RabbitMqPublicationConfigurator();
        configure.Invoke(cfg);
        
        _publications.Add(cfg.ToPublication());
        return this;
    }

    internal IEnumerable<RabbitMqPublication> ToPublications()
    {
        return _publications;
    }
}