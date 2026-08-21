using System;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures the most important settings of the RabbitMQ <see cref="ConnectionFactory"/>
/// used to establish connections to the broker.
/// </summary>
public class RabbitMqConnectionConfigurator
{
    private Uri? _uri;

    /// <summary>
    /// Sets the full AMQP URI used to connect to the broker, including credentials,
    /// virtual host and TLS scheme (e.g. <c>amqps://user:pass@host:5671/vhost</c>).
    /// </summary>
    /// <param name="uri">The AMQP connection URI.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator Uri(Uri uri)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        return this;
    }

    private string? _hostName;

    /// <summary>
    /// Sets the host name of the RabbitMQ broker.
    /// </summary>
    /// <param name="hostName">The broker host name. Defaults to <c>localhost</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator HostName(string hostName)
    {
        if (string.IsNullOrEmpty(hostName))
        {
            throw new ArgumentException("Host name cannot be null or empty.", nameof(hostName));
        }

        _hostName = hostName;
        return this;
    }

    private int? _port;

    /// <summary>
    /// Sets the port of the RabbitMQ broker.
    /// </summary>
    /// <param name="port">The broker port. Defaults to the AMQP default (5672, or 5671 for TLS) when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator Port(int port)
    {
        _port = port;
        return this;
    }

    private string? _virtualHost;

    /// <summary>
    /// Sets the virtual host to connect to.
    /// </summary>
    /// <param name="virtualHost">The virtual host name. Defaults to <c>/</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator VirtualHost(string virtualHost)
    {
        if (string.IsNullOrEmpty(virtualHost))
        {
            throw new ArgumentException("Virtual host cannot be null or empty.", nameof(virtualHost));
        }

        _virtualHost = virtualHost;
        return this;
    }

    private string? _userName;
    private string? _password;

    /// <summary>
    /// Sets the credentials used to authenticate with the broker.
    /// </summary>
    /// <param name="userName">The user name. Defaults to <c>guest</c> when not set.</param>
    /// <param name="password">The password. Defaults to <c>guest</c> when not set.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator Credentials(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentException("User name cannot be null or empty.", nameof(userName));
        }

        _userName = userName;
        _password = password ?? throw new ArgumentNullException(nameof(password));
        return this;
    }

    private string? _clientProvidedName;

    /// <summary>
    /// Sets a human-readable name for the connection, shown in the RabbitMQ management UI.
    /// </summary>
    /// <param name="clientProvidedName">The connection name.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator ClientProvidedName(string clientProvidedName)
    {
        _clientProvidedName = clientProvidedName;
        return this;
    }

    private bool? _automaticRecoveryEnabled;
    private bool? _topologyRecoveryEnabled;
    private TimeSpan? _networkRecoveryInterval;

    /// <summary>
    /// Enables or disables automatic connection and topology recovery after network failures.
    /// </summary>
    /// <param name="enabled">Whether automatic recovery is enabled. Enabled by default by the client.</param>
    /// <param name="networkRecoveryInterval">
    /// Optional interval between recovery attempts. Defaults to the client default when not set.
    /// </param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator AutomaticRecovery(bool enabled = true, TimeSpan? networkRecoveryInterval = null)
    {
        _automaticRecoveryEnabled = enabled;
        _topologyRecoveryEnabled = enabled;
        _networkRecoveryInterval = networkRecoveryInterval;
        return this;
    }

    private TimeSpan? _requestedHeartbeat;

    /// <summary>
    /// Sets the heartbeat interval requested during connection negotiation.
    /// </summary>
    /// <param name="requestedHeartbeat">The requested heartbeat interval.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator RequestedHeartbeat(TimeSpan requestedHeartbeat)
    {
        _requestedHeartbeat = requestedHeartbeat;
        return this;
    }

    private TimeSpan? _requestedConnectionTimeout;

    /// <summary>
    /// Sets the timeout requested during connection negotiation.
    /// </summary>
    /// <param name="requestedConnectionTimeout">The requested connection timeout.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator RequestedConnectionTimeout(TimeSpan requestedConnectionTimeout)
    {
        _requestedConnectionTimeout = requestedConnectionTimeout;
        return this;
    }

    private bool _sslEnabled;
    private string? _sslServerName;

    /// <summary>
    /// Enables or disables TLS (SSL) for the connection.
    /// </summary>
    /// <param name="enabled">Whether TLS is enabled.</param>
    /// <param name="serverName">
    /// Optional TLS server name (SNI / certificate validation). Defaults to the host name when not set.
    /// </param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator UseSsl(bool enabled = true, string? serverName = null)
    {
        _sslEnabled = enabled;
        _sslServerName = serverName;
        return this;
    }

    private Action<ConnectionFactory>? _configure;

    /// <summary>
    /// Registers a callback to configure any other <see cref="ConnectionFactory"/> setting
    /// not covered by this configurator. Runs after all other settings have been applied.
    /// </summary>
    /// <param name="configure">A delegate that receives the <see cref="ConnectionFactory"/> being configured.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqConnectionConfigurator Configure(Action<ConnectionFactory> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    internal void ApplyTo(ConnectionFactory connectionFactory)
    {
        if (_uri is not null)
        {
            connectionFactory.Uri = _uri;
        }

        if (_hostName is not null)
        {
            connectionFactory.HostName = _hostName;
        }

        if (_port.HasValue)
        {
            connectionFactory.Port = _port.Value;
        }

        if (_virtualHost is not null)
        {
            connectionFactory.VirtualHost = _virtualHost;
        }

        if (_userName is not null)
        {
            connectionFactory.UserName = _userName;
        }

        if (_password is not null)
        {
            connectionFactory.Password = _password;
        }

        if (_clientProvidedName is not null)
        {
            connectionFactory.ClientProvidedName = _clientProvidedName;
        }

        if (_automaticRecoveryEnabled.HasValue)
        {
            connectionFactory.AutomaticRecoveryEnabled = _automaticRecoveryEnabled.Value;
        }

        if (_topologyRecoveryEnabled.HasValue)
        {
            connectionFactory.TopologyRecoveryEnabled = _topologyRecoveryEnabled.Value;
        }

        if (_networkRecoveryInterval.HasValue)
        {
            connectionFactory.NetworkRecoveryInterval = _networkRecoveryInterval.Value;
        }

        if (_requestedHeartbeat.HasValue)
        {
            connectionFactory.RequestedHeartbeat = _requestedHeartbeat.Value;
        }

        if (_requestedConnectionTimeout.HasValue)
        {
            connectionFactory.RequestedConnectionTimeout = _requestedConnectionTimeout.Value;
        }

        if (_sslEnabled || _sslServerName is not null)
        {
            connectionFactory.Ssl.Enabled = _sslEnabled;

            if (_sslServerName is not null)
            {
                connectionFactory.Ssl.ServerName = _sslServerName;
            }
        }

        _configure?.Invoke(connectionFactory);
    }
}
