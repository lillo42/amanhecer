namespace Amanhecer.RabbitMq;

/// <summary>
/// Names of the metadata entries used to tune RabbitMQ publications from the pipeline context.
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
}