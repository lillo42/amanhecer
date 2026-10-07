namespace Amanhecer.RabbitMq.Streams;

/// <summary>
/// Well-known keys for <see cref="Amanhecer.Abstractions.Messaging.Message.Metadata"/> entries set by the RabbitMQ Streams transport.
/// </summary>
public static class Metadata
{
    /// <summary>
    /// Key for the AMQP <c>AbsoluteExpiryTime</c> property stored as a <see cref="System.DateTime"/>.
    /// Read from <see cref="Amanhecer.Abstractions.Messaging.Message.Metadata"/> when republishing a
    /// consumed message, or set on the pipeline context metadata to apply an expiration at publish
    /// time (a <see cref="System.DateTime"/>, <see cref="System.DateTimeOffset"/>
    /// or <see cref="System.TimeSpan"/> from now).
    /// </summary>
    public const string Expiration = "Amanhecer.RabbitMq.Streams.Expiration";

    /// <summary>
    /// Key for the AMQP <c>GroupSequence</c> property stored as a <see cref="uint"/>.
    /// </summary>
    public const string GroupSequence = "Amanhecer.RabbitMq.Streams.GroupSequence";

    /// <summary>
    /// Key for the AMQP <c>ReplyToGroupId</c> property stored as a <see cref="string"/>.
    /// </summary>
    public const string ReplyToGroupId = "Amanhecer.RabbitMq.Streams.ReplyToGroupId";

    /// <summary>
    /// Key for the AMQP <c>To</c> property stored as a <see cref="string"/>.
    /// </summary>
    public const string To = "Amanhecer.RabbitMq.Streams.To";

    /// <summary>
    /// Key for the underlying <c>RawConsumer</c> instance used for offset commits.
    /// </summary>
    public const string Consumer = "Amanhecer.RabbitMq.Streams.Consumer";
    
    /// <summary>
    /// Key for the <c>MessageContext</c> containing the stream offset.
    /// </summary>
    public const string Context = "Amanhecer.RabbitMq.Streams.Context";
    
    /// <summary>
    /// Key for the original <c>RabbitMQ.Stream.Client.Message</c> received from the broker.
    /// </summary>
    public const string OriginalMessage = "Amanhecer.RabbitMq.Streams.Message";
    
    /// <summary>
    /// Key for the stream name the message was consumed from.
    /// </summary>
    public const string Stream = "Amanhecer.RabbitMq.Streams.Stream";
}