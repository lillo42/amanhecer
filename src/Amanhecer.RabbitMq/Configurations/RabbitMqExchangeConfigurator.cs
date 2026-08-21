using System;
using Amanhecer.RabbitMq.Provisioners;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures a RabbitMQ exchange: its name and how it is provisioned on the broker.
/// </summary>
public class RabbitMqExchangeConfigurator
{
    private string? _name;

    /// <summary>
    /// Sets the name of the exchange.
    /// </summary>
    /// <param name="name">The exchange name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    public RabbitMqExchangeConfigurator Name(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Exchange name cannot be null or empty.", nameof(name));
        }

        _name = name;
        return this;
    }

    private IExchangeProvisioner _provisioner = new AssumeExchangeExists();

    /// <summary>
    /// Sets a custom <see cref="IExchangeProvisioner"/> used to provision the exchange on the broker.
    /// </summary>
    /// <param name="provisioner">The provisioner to use.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqExchangeConfigurator Provisioner(IExchangeProvisioner provisioner)
    {
        _provisioner = provisioner;
        return this;
    }

    /// <summary>
    /// Assumes the exchange already exists on the broker and performs no provisioning.
    /// This is the default behaviour.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqExchangeConfigurator AssumeExists()
    {
        _provisioner = new AssumeExchangeExists();
        return this;
    }

    /// <summary>
    /// Validates that the exchange exists on the broker, throwing when it does not.
    /// </summary>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqExchangeConfigurator ValidateIfExists()
    {
        _provisioner = new ValidateExchangeExists();
        return this;
    }

    /// <summary>
    /// Declares the exchange on the broker when it does not already exist.
    /// </summary>
    /// <param name="configure">A delegate that configures how the exchange is declared.</param>
    /// <returns>The configurator instance for method chaining.</returns>
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
            throw new InvalidOperationException(
                "An exchange name is required. Call Name to configure it.");
        }

        return new Exchange
        {
            Name = _name,
            Provisioner = _provisioner
        };
    }

    /// <summary>
    /// Configures how the exchange is declared on the broker when it does not already exist.
    /// </summary>
    public class CreateIfNotExistsConfigurator
    {
        private string _type;

        /// <summary>
        /// Sets the exchange type (e.g. <c>direct</c>, <c>fanout</c>, <c>topic</c>, <c>headers</c>).
        /// </summary>
        /// <param name="type">The exchange type.</param>
        /// <returns>The configurator instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is null or empty.</exception>
        public CreateIfNotExistsConfigurator Type(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("Exchange type cannot be null or empty.", nameof(type));
            }

            _type = type;
            return this;
        }

        private bool _durable;

        /// <summary>
        /// Sets whether the declared exchange survives a broker restart.
        /// </summary>
        /// <param name="durable">Whether the exchange is durable.</param>
        /// <returns>The configurator instance for method chaining.</returns>
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