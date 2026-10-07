using System;
using System.Text;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Stream.Client.Reliable;

namespace Amanhecer.RabbitMq.Streams;

/// <summary>RabbitMQ Streams publication options, extending <see cref="Publication"/> with stream-specific settings.</summary>
public class RabbitMqStreamPublication(string stream) : Publication
{
    /// <summary>
    /// Gets or sets the user id applied to published messages.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the stream name to publish to.</summary>
    public string Stream { get; set; } = stream;

    /// <summary>Gets or sets an optional callback to further configure the <see cref="ProducerConfig"/> before the producer is created.</summary>
    public Action<ProducerConfig>? Configure { get; set; }

    /// <summary>Gets or sets the encoding used to serialize string values such as <see cref="UserId"/>. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}