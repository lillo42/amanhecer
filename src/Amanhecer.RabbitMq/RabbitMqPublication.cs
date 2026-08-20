using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

public class RabbitMqPublication : Publication
{
    public required string RabbitMqRoutingKey { get; set; }
    public Exchange? Exchange { get; set; }
    public bool Mandatory { get; set; }
    public string ContentEncoding { get; set; } = "utf-8";
    public bool Persistent { get; set; }
    public string? UserId { get; set; }
    public string? AppId { get; set; }
    public string? ClusterId { get; set; }

#if !NETFRAMEWORK
    public RabbitMQ.Client.CreateChannelOptions? ChannelOptions { get; set; }
#endif
}