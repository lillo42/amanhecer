using System;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// A subscription that consumes messages from a Kafka topic.
/// </summary>
public class ConfluentKafkaSubscription : Subscription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfluentKafkaSubscription"/> class that
    /// consumes messages from the given topic, with <see cref="Subscription.MessagingSystem"/>
    /// set to <c>kafka</c>.
    /// </summary>
    /// <param name="toRoutingKey">The routing key consumed messages are dispatched to.</param>
    /// <param name="topic">The name of the topic messages are consumed from.</param>
    /// <param name="groupId">The consumer group the consumers join.</param>
    public ConfluentKafkaSubscription(string toRoutingKey, string topic, string groupId) : base(toRoutingKey)
    {
        Topic = topic;
        GroupId = groupId;
    }

    /// <inheritdoc/>
    public override string MessagingSystem => "kafka";

    /// <summary>
    /// Gets or sets the name of the topic messages are consumed from.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the consumer group the consumers of this subscription join. Consumers in
    /// the same group share the topic partitions between them.
    /// </summary>
    public string GroupId { get; set; }

    /// <summary>
    /// Gets or sets where consumption starts when there is no committed offset for the
    /// consumer group. Defaults to <see cref="Confluent.Kafka.AutoOffsetReset.Earliest"/>.
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    /// <summary>
    /// Gets or sets how many acknowledged offsets are stored before they are committed to
    /// the broker. Defaults to <c>10</c>. A higher value reduces commit traffic but replays
    /// more messages when the consumer stops without flushing; a lower value does the
    /// opposite. See also <see cref="SweepUncommittedOffsetsInterval"/>.
    /// </summary>
    public long CommitBatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the interval after which stored-but-uncommitted offsets are committed by
    /// the sweeper, so partially complete batches on low-traffic topics do not linger
    /// uncommitted. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan SweepUncommittedOffsetsInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ConsumerConfig"/> before the
    /// consumer of this subscription is created. It runs after the gateway's
    /// <see cref="ConfluentKafkaGateway.ConfigureConsumer"/> callback, so it can override the
    /// gateway-wide configuration for this subscription alone.
    /// </summary>
    public Action<ConsumerConfig>? Configure { get; set; }
}
