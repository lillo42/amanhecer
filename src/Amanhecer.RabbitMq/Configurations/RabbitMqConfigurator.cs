using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Configurations;

public class RabbitMqConfigurator
{
    private ConnectionFactory? _connectionFactory;

    public RabbitMqConfigurator Connection(Action<RabbitMqConnectionConfigurator> configure)
    {
        var cfg = new RabbitMqConnectionConfigurator();
        configure.Invoke(cfg);

        _connectionFactory = new ConnectionFactory();
        cfg.ApplyTo(_connectionFactory);
        return this;
    }


    private readonly List<RabbitMqPublication> _publications = [];

    public RabbitMqConfigurator Publications(Action<RabbitMqPublicationsConfigurator> configure)
    {
        var cfg = new RabbitMqPublicationsConfigurator();
        configure.Invoke(cfg);

        _publications.AddRange(cfg.ToPublications());
        return this;
    }

    internal IGateway CreateGateway()
    {
        return new RabbitMqGateway
        {
            Publications = _publications
        };
    }
}