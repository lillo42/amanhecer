using System;
using System.Collections.Generic;
using System.Net;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures the connection settings used to create a <see cref="StreamSystem"/> that
/// connects to a RabbitMQ Streams broker.
/// </summary>
public class RabbitMqStreamConnectionConfigurator
{
    private string? _userName;

    /// <summary>
    /// Sets the user name used to authenticate with the broker. Defaults to <c>guest</c>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator UserName(string userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentException("User name cannot be null or empty.", nameof(userName));
        }

        _userName = userName;
        return this;
    }

    private string? _password;

    /// <summary>
    /// Sets the password used to authenticate with the broker. Defaults to <c>guest</c>.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator Password(string password)
    {
        _password = password ?? throw new ArgumentNullException(nameof(password));
        return this;
    }

    /// <summary>
    /// Sets the credentials used to authenticate with the broker.
    /// </summary>
    /// <param name="userName">The user name. Defaults to <c>guest</c> when not set.</param>
    /// <param name="password">The password. Defaults to <c>guest</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator Credentials(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentException("User name cannot be null or empty.", nameof(userName));
        }

        _userName = userName;
        _password = password ?? throw new ArgumentNullException(nameof(password));
        return this;
    }

    private string? _virtualHost;

    /// <summary>
    /// Sets the virtual host to connect to. Defaults to <c>/</c>.
    /// </summary>
    /// <param name="virtualHost">The virtual host name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator VirtualHost(string virtualHost)
    {
        if (string.IsNullOrEmpty(virtualHost))
        {
            throw new ArgumentException("Virtual host cannot be null or empty.", nameof(virtualHost));
        }

        _virtualHost = virtualHost;
        return this;
    }

    private string? _userId;

    /// <summary>
    /// Sets the default user id stamped on every published message.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator UserId(string userId)
    {
        _userId = userId;
        return this;
    }

    private readonly List<EndPoint> _endPoints = [];

    /// <summary>
    /// Adds an endpoint (host and port) the stream system connects to. When no endpoint is
    /// added, the system defaults to <c>localhost:5552</c>.
    /// </summary>
    /// <param name="host">The broker host name or IP address.</param>
    /// <param name="port">The broker port. Defaults to <c>5552</c>.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator Endpoint(string host, int port = 5552)
    {
        if (string.IsNullOrEmpty(host))
        {
            throw new ArgumentException("Host cannot be null or empty.", nameof(host));
        }

        _endPoints.Add(new DnsEndPoint(host, port));
        return this;
    }

    /// <summary>
    /// Adds an endpoint the stream system connects to. When no endpoint is added, the system
    /// defaults to <c>localhost:5552</c>.
    /// </summary>
    /// <param name="endPoint">The endpoint.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator Endpoint(EndPoint endPoint)
    {
        _endPoints.Add(endPoint ?? throw new ArgumentNullException(nameof(endPoint)));
        return this;
    }

    private Action<StreamSystemConfig>? _configure;

    /// <summary>
    /// Registers a callback to configure any other <see cref="StreamSystemConfig"/> setting
    /// not covered by this configurator. Runs after all other settings have been applied.
    /// </summary>
    /// <param name="configure">A delegate that receives the <see cref="StreamSystemConfig"/> being configured.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamConnectionConfigurator Configure(Action<StreamSystemConfig> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    internal void ApplyTo(RabbitMqStreamGateway gateway)
    {
        if (_userName is not null)
        {
            gateway.UserName = _userName;
        }

        if (_password is not null)
        {
            gateway.Password = _password;
        }

        if (_virtualHost is not null)
        {
            gateway.VirtualHost = _virtualHost;
        }

        if (_userId is not null)
        {
            gateway.UserId = _userId;
        }

        if (_endPoints.Count > 0)
        {
            gateway.EndPoints = _endPoints;
        }

        if (_configure is not null)
        {
            gateway.Configuration = _configure;
        }
    }
}
