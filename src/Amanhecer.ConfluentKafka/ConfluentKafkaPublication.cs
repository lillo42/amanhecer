using System;
using System.Text;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// A publication that produces messages to a Kafka topic.
/// </summary>
public class ConfluentKafkaPublication : Publication
{
    /// <summary>
    /// Gets or sets the name of the topic messages are produced to.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets the encoding used to encode string message headers and CloudEvents attributes
    /// into record header bytes. Defaults to UTF-8.
    /// </summary>
    public Encoding Encoding { get; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets the converter applied to message header values of a type the producer
    /// does not know how to encode.
    /// </summary>
    public Func<object, byte[]> ConvertToByteArray { get; set; } = _ => throw new NotSupportedException(
        $"{nameof(ConvertToByteArray)} must be configured to encode custom Kafka header values.");

    /// <summary>
    /// Gets or sets a value indicating whether publishing waits for the broker's delivery
    /// confirmation. Defaults to <see langword="false"/>: the message is queued to the
    /// producer buffer and publishing completes without delivery guarantees (fire and
    /// forget). Set it to <see langword="true"/> to await the delivery report, so broker
    /// errors surface as publish exceptions and the returned task only completes once the
    /// record is acknowledged.
    /// </summary>
    public bool WaitForConfirmation { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked with the <see cref="ProducerConfig"/> before the
    /// producer of this publication is created. It runs after the gateway's
    /// <see cref="ConfluentKafkaGateway.ConfigureProducer"/> callback, so it can override the
    /// gateway-wide configuration for this publication alone. Defaults to selecting the
    /// <see cref="Partitioner.Murmur2Random"/> partitioner, which maps keys to partitions the
    /// same way the Java client does; replacing the callback drops that default, so set the
    /// partitioner again when other clients have to agree on the partition a key lands on.
    /// </summary>
    public Action<ProducerConfig> ConfigureProducer { get; set; } = cfg =>
    {
        cfg.Partitioner = Partitioner.Murmur2Random;
    };
}
