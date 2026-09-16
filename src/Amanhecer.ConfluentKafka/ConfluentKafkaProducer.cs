using System;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// An <see cref="IProducer"/> that produces messages to a Kafka topic through a
/// Confluent.Kafka producer client.
/// </summary>
/// <param name="producer">The producer client shared by the publications of a gateway.</param>
public class ConfluentKafkaProducer(IProducer<string?, byte[]> producer) : IProducer, IDisposable
{
    /// <inheritdoc />
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context)
    {
        if (publication is not ConfluentKafkaPublication kafkaPublication)
        {
            return;
        }

        if (kafkaPublication.WaitForConfirmation)
        {
            // Await the delivery report: broker errors surface as publish exceptions and the
            // task only completes once the record is acknowledged.
            await producer.ProduceAsync(kafkaPublication.Topic,
                ToKafkaMessage(message, kafkaPublication));
            return;
        }

        producer.Produce(kafkaPublication.Topic,
            ToKafkaMessage(message, kafkaPublication));
    }

    private static Message<string?, byte[]> ToKafkaMessage(Message message,
        ConfluentKafkaPublication publication)
    {
        var kafkaMessage = new Message<string?, byte[]>
        {
            Key = message.PartitionKey,
            Value = message.Payload.ToArray(),
            Headers = []
        };

        foreach (var header in message.Headers)
        {
            kafkaMessage.Headers.Add(header.Key,
                ToBinary(header.Value,
                    publication.Encoding,
                    publication.ConvertToByteArray));
        }

        if (publication.CloudEventType == CloudEventType.Binary)
        {
            kafkaMessage.Headers.Add("ce_id", publication.Encoding.GetBytes(message.Id));
            kafkaMessage.Headers.Add("ce_time", publication.Encoding.GetBytes(message.Time.ToString("O")));

            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                kafkaMessage.Headers.Add("ce_correlationid",
                    publication.Encoding.GetBytes(message.CorrelationId));
            }

            if (message.Baggage != null)
            {
                kafkaMessage.Headers.Add("ce_baggage",
                    publication.Encoding.GetBytes(message.Baggage.ToString()));
            }

            if (message.ContentType != null)
            {
                kafkaMessage.Headers.Add("ce_datacontenttype",
                    publication.Encoding.GetBytes(message.ContentType.ToString()));
            }

            if (!string.IsNullOrEmpty(message.DataRef))
            {
                kafkaMessage.Headers.Add("ce_dataref", publication.Encoding.GetBytes(message.DataRef));
            }

            if (message.DataSchema != null)
            {
                kafkaMessage.Headers.Add("ce_dataschema", publication.Encoding.GetBytes(message.DataSchema.ToString()));
            }

            if (!string.IsNullOrEmpty(message.ReplyTo))
            {
                kafkaMessage.Headers.Add("ce_replyto", publication.Encoding.GetBytes(message.ReplyTo));
            }

            if (!string.IsNullOrEmpty(message.Subject))
            {
                kafkaMessage.Headers.Add("ce_subject", publication.Encoding.GetBytes(message.Subject));
            }

            if (!string.IsNullOrEmpty(message.SpecVersion))
            {
                kafkaMessage.Headers.Add("ce_specversion", publication.Encoding.GetBytes(message.SpecVersion));
            }

            if (message.Source != null)
            {
                kafkaMessage.Headers.Add("ce_source", publication.Encoding.GetBytes(message.Source.ToString()));
            }
            
            if (!string.IsNullOrEmpty(message.Type))
            {
                kafkaMessage.Headers.Add("ce_type", publication.Encoding.GetBytes(message.Type));
            }
            
            if (!string.IsNullOrEmpty(message.TraceParent))
            {
                kafkaMessage.Headers.Add("ce_traceparent", publication.Encoding.GetBytes(message.TraceParent));
            }
            
            if (message.TraceState != null)
            {
                kafkaMessage.Headers.Add("ce_tracestate", publication.Encoding.GetBytes(message.TraceState.ToString()));
            }  
        }

        return kafkaMessage;
    }

    private static byte[] ToBinary(object? obj, Encoding encoding, Func<object, byte[]> converter)
    {
        if (obj == null)
        {
            return [];
        }

        if (obj is byte b)
        {
            return [b];
        }

        if (obj is byte[] bArr)
        {
            return bArr;
        }

        if (obj is sbyte sb)
        {
            return [(byte)sb];
        }

        if (obj is bool bo)
        {
            return BitConverter.GetBytes(bo);
        }

        if (obj is char c)
        {
            return BitConverter.GetBytes(c);
        }

        if (obj is char[] cArray)
        {
            return encoding.GetBytes(cArray);
        }

        if (obj is string s)
        {
            return encoding.GetBytes(s);
        }

        if (obj is short sh)
        {
            return BitConverter.GetBytes(sh);
        }

        if (obj is ushort ush)
        {
            return BitConverter.GetBytes(ush);
        }

        if (obj is int i)
        {
            return BitConverter.GetBytes(i);
        }

        if (obj is uint ui)
        {
            return BitConverter.GetBytes(ui);
        }

        if (obj is long l)
        {
            return BitConverter.GetBytes(l);
        }

        if (obj is ulong ul)
        {
            return BitConverter.GetBytes(ul);
        }

        if (obj is double d)
        {
            return BitConverter.GetBytes(d);
        }

        if (obj is float f)
        {
            return BitConverter.GetBytes(f);
        }

        if (obj is decimal de)
        {
            var bits = decimal.GetBits(de);
            var bytes = new byte[16];
            Buffer.BlockCopy(bits, 0, bytes, 0, 16);
            return bytes;
        }

#if NET8_0_OR_GREATER
        if (obj is DateOnly dateOnly)
        {
            return encoding.GetBytes(dateOnly.ToString("O"));
        }

        if (obj is TimeOnly timeOnly)
        {
            return encoding.GetBytes(timeOnly.ToString("O"));
        }
#endif

#if NET9_0_OR_GREATER
        if (obj is Half h)
        {
            return BitConverter.GetBytes(h);
        }

        if (obj is Int128 i128)
        {
            return BitConverter.GetBytes(i128);
        }

        if (obj is UInt128 ui128)
        {
            return BitConverter.GetBytes(ui128);
        }
#endif

        if (obj is DateTimeOffset dto)
        {
            return encoding.GetBytes(dto.ToString("O"));
        }

        if (obj is DateTime dt)
        {
            return encoding.GetBytes(dt.ToUniversalTime().ToString("O"));
        }

        if (obj is Guid guid)
        {
            return guid.ToByteArray();
        }

        if (obj is TimeSpan timeSpan)
        {
            return BitConverter.GetBytes(timeSpan.Ticks);
        }

        if (obj is Uri uri)
        {
            return encoding.GetBytes(uri.ToString());
        }

        return converter(obj);
    }


    /// <inheritdoc />
    public void Dispose()
    {
        producer.Flush();
        producer.Dispose();
    }
}