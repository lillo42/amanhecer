using System;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams;

/// <summary>
/// RabbitMQ Streams subscription configuration, extending <see cref="Subscription"/> with stream-specific settings.
/// </summary>
public class RabbitMqStreamSubscription(string toRoutingKey, string stream) : Subscription(toRoutingKey)
{
    /// <summary>
    /// Gets or sets the stream name to consume from.
    /// </summary>
    public string Stream { get; set; } = stream;

    /// <summary>
    /// Gets or sets an optional callback to further configure the <see cref="ConsumerConfig"/>
    /// before the consumer is created.
    /// </summary>
    public Action<ConsumerConfig>? Configuration { get; set; }
}

/// <summary>
/// A RabbitMQ Streams subscription that consumes from all partitions of a super stream: each
/// partition is read through a single-active consumer and offsets are tracked per partition.
/// </summary>
/// <param name="toRoutingKey">The routing key consumed messages are dispatched to.</param>
/// <param name="stream">The name of the super stream to consume from.</param>
public class RabbitMqSuperStreamSubscription(string toRoutingKey, string stream)
    : RabbitMqStreamSubscription(toRoutingKey, stream);
