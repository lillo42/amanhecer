namespace Amanhecer.ConfluentKafka;

/// <summary>
/// The keys of the Kafka-specific entries stamped into <see cref="Amanhecer.Abstractions.Messaging.Message.Metadata"/>.
/// </summary>
public static class MetadataName
{
    /// <summary>
    /// The <see cref="Confluent.Kafka.TopicPartitionOffset"/> of the consumed record, used to
    /// settle (commit or seek) the message.
    /// </summary>
    public const string TopicPartitionOffset = "kafka.topicpartitionoffset";

    /// <summary>
    /// The topic the message was consumed from.
    /// </summary>
    public const string Topic = "kafka.topic";

    /// <summary>
    /// The partition the message was consumed from.
    /// </summary>
    public const string Partition = "kafka.partition";

    /// <summary>
    /// The offset of the message in the partition.
    /// </summary>
    public const string Offset = "kafka.offset";
}
