namespace Amanhecer.RabbitMq;

/// <summary>
/// Names of the metadata entries used to tune RabbitMQ publications from the pipeline context,
/// and of the metadata entries stamped on consumed messages.
/// </summary>
public static class MetadataName
{
    /// <summary>
    /// The content encoding applied to the published message.
    /// </summary>
    public const string ContentEncoding = "Amanhecer.RabbitMq.ContentEncoding";

    /// <summary>
    /// The expiration applied to the published message.
    /// </summary>
    public const string Expiration = "Amanhecer.RabbitMq.Expiration";

    /// <summary>
    /// Whether the published message is persisted to disk by the broker.
    /// </summary>
    public const string Persistent = "Amanhecer.RabbitMq.Persistent";

    /// <summary>
    /// The priority applied to the published message.
    /// </summary>
    public const string Priority = "Amanhecer.RabbitMq.Priority";

    /// <summary>
    /// The message id assigned by the publisher of the consumed message.
    /// </summary>
    public const string MessageId = "Amanhecer.RabbitMq.MessageId";

    /// <summary>
    /// The tag identifying the consumer that received the consumed message.
    /// </summary>
    public const string ConsumerTag = "Amanhecer.RabbitMq.ConsumerTag";

    /// <summary>
    /// The delivery tag used to settle (ack, nack or defer) the consumed message.
    /// </summary>
    public const string DeliveryTag = "Amanhecer.RabbitMq.DeliveryTag";

    /// <summary>
    /// Whether the consumed message has been delivered before.
    /// </summary>
    public const string Redelivered = "Amanhecer.RabbitMq.Redelivered";

    /// <summary>
    /// The exchange the consumed message was published to.
    /// </summary>
    public const string Exchange = "Amanhecer.RabbitMq.Exchange";

    /// <summary>
    /// The routing key the consumed message was published with.
    /// </summary>
    public const string RoutingKey = "Amanhecer.RabbitMq.RoutingKey";
}
