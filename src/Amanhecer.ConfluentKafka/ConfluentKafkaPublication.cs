using System;
using System.Text;
using System.Text.Json;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// A publication that produces messages to a Kafka topic.
/// </summary>
public class ConfluentKafkaPublication : Publication
{
    /// <summary>
    /// Gets or sets the name of the topic messages are produced to.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets the encoding used to encode string message headers and CloudEvents attributes
    /// into record header bytes. Defaults to UTF-8.
    /// </summary>
    public Encoding Encoding { get; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets the converter applied to message header values of a type the producer
    /// does not know how to encode. Defaults to JSON serialization.
    /// </summary>
    public Func<object, byte[]> ConvertToByteArray { get; set; } = obj => JsonSerializer.SerializeToUtf8Bytes(obj);

    /// <summary>
    /// Gets or sets a value indicating whether publishing waits for the broker's delivery
    /// confirmation. Defaults to <see langword="false"/>: the message is queued to the
    /// producer buffer and publishing completes without delivery guarantees (fire and
    /// forget). Set it to <see langword="true"/> to await the delivery report, so broker
    /// errors surface as publish exceptions and the returned task only completes once the
    /// record is acknowledged.
    /// </summary>
    public bool WaitForConfirmation { get; set; }
}
