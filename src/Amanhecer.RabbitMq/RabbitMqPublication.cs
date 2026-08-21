using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

/// <summary>
/// A publication that publishes messages to a RabbitMQ exchange.
/// </summary>
public class RabbitMqPublication : Publication
{
    /// <summary>
    /// Gets or sets the RabbitMQ routing key messages are published with.
    /// </summary>
    public required string RabbitMqRoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the exchange messages are published to.
    /// </summary>
    public Exchange? Exchange { get; set; }

    /// <summary>
    /// Gets or sets whether messages are published with the mandatory flag, requiring the
    /// broker to route them to at least one queue.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// Gets or sets the content encoding applied to published messages. Defaults to "utf-8".
    /// </summary>
    public string ContentEncoding { get; set; } = "utf-8";

    /// <summary>
    /// Gets or sets whether published messages are persisted to disk by the broker.
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Gets or sets the user id applied to published messages.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the application id applied to published messages.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Gets or sets the cluster id applied to published messages.
    /// </summary>
    public string? ClusterId { get; set; }

#if !NETFRAMEWORK
    /// <summary>
    /// Gets or sets the options used to create the channel this publication publishes through.
    /// </summary>
    public RabbitMQ.Client.CreateChannelOptions? ChannelOptions { get; set; }
#endif
}