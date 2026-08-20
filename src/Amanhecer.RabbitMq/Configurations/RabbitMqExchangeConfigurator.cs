using System;
using Amanhecer.RabbitMq.Provisioners;

namespace Amanhecer.RabbitMq.Configurations;

public class RabbitMqExchangeConfigurator
{
    private string? _name;

    public RabbitMqExchangeConfigurator Name(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new NotImplementedException();
        }

        _name = name;
        return this;
    }

    private IExchangeProvisioner _provisioner = new AssumeExchangeExists();

    public RabbitMqExchangeConfigurator Provisioner(IExchangeProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    public RabbitMqExchangeConfigurator AssumeExists()
    {
        _provisioner = new AssumeExchangeExists();
        return this;
    }

    public RabbitMqExchangeConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateExchangeExists();
        return this;
    }

    public RabbitMqExchangeConfigurator CreateIfNotExists(Action<CreateIfNotExistsConfigurator> configure)
    {
        var cfg = new CreateIfNotExistsConfigurator();
        configure.Invoke(cfg);
        _provisioner = cfg.ToProvisioner();
        return this;
    }

    internal Exchange ToExchange()
    {
        if (string.IsNullOrEmpty(_name))
        {
            throw new NotImplementedException();
        }

        return new Exchange
        {
            Name = _name,
            Provisioner = _provisioner
        };
    }

    public class CreateIfNotExistsConfigurator
    {
        private string _type;

        public CreateIfNotExistsConfigurator Type(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new NotImplementedException();
            }

            _type = type;
            return this;
        }

        private bool _durable;

        public CreateIfNotExistsConfigurator Durable(bool durable)
        {
            _durable = durable;
            return this;
        }

        internal CreateIfNotExchange ToProvisioner()
        {
            return new CreateIfNotExchange
            {
                Type = _type,
                Durable = _durable
            };
        }
    }
}