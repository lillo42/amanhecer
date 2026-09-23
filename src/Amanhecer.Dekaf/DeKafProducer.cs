using System;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Amanhecer.Dekaf;

/// <summary>
/// An <see cref="IProducer"/> that produces messages to a Kafka topic through a Dekaf
/// producer client.
/// </summary>
/// <param name="producer">The producer client shared by the publications of a gateway.</param>
public class DeKafProducer(IKafkaProducer<string, byte[]> producer) : IProducer, IAsyncDisposable
{
    /// <inheritdoc/>
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context)
    {
        if (publication is not DekafPublication dekafPublication)
        {
            return;
        }

        InjectCloudEventHeaders(message);
        var producerMessage = ToDeKafMessage(message, dekafPublication);

        await producer.InitializeAsync().ConfigureAwait(context.ContinueOnCapturedContext);
        if (dekafPublication.WaitForConfirmation)
        {
            // Await the delivery result: broker errors surface as publish exceptions and the
            // task only completes once the record is acknowledged.
            await producer.ProduceAsync(producerMessage).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        await producer.FireAsync(producerMessage).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private static void InjectCloudEventHeaders(Message message)
    {
        message.Headers.Add("ce_id", message.Id);
        message.Headers.Add("ce_time", message.Time);
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            message.Headers.Add("ce_correlationid", message.CorrelationId);
        }

        if (message.Baggage != null)
        {
            message.Headers.Add("ce_baggage", message.Baggage.ToString());
        }

        if (message.ContentType != null)
        {
            message.Headers.Add("ce_datacontenttype", message.ContentType.ToString());
        }

        if (!string.IsNullOrEmpty(message.ContentEncoding))
        {
            message.Headers.Add("Content-Encoding", message.ContentEncoding);
        }

        if (!string.IsNullOrEmpty(message.DataRef))
        {
            message.Headers.Add("ce_dataref", message.DataRef);
        }

        if (message.DataSchema != null)
        {
            message.Headers.Add("ce_dataschema", message.DataSchema.ToString());
        }

        if (!string.IsNullOrEmpty(message.ReplyTo))
        {
            message.Headers.Add("ce_replyto", message.ReplyTo);
        }

        if (!string.IsNullOrEmpty(message.Subject))
        {
            message.Headers.Add("ce_subject", message.Subject);
        }

        message.Headers.Add("ce_specversion", message.SpecVersion);
        message.Headers.Add("ce_source", message.Source.ToString());
        message.Headers.Add("ce_type", message.Type);

        if (!string.IsNullOrEmpty(message.TraceParent))
        {
            message.Headers.Add("ce_traceparent", message.TraceParent);
        }

        if (message.TraceState != null)
        {
            message.Headers.Add("ce_tracestate", message.TraceState.ToString());
        }
    }

    private static ProducerMessage<string, byte[]> ToDeKafMessage(Message message,
        DekafPublication publication)
    {
        var headers = new Headers();

        foreach (var header in message.Headers)
        {
            headers.Add(header.Key,
                ToBinary(header.Value,
                    publication.Encoding,
                    publication.ConvertToByteArray));
        }

        return new ProducerMessage<string, byte[]>
        {
            Topic = publication.Topic,
            Key = message.PartitionKey,
            Value = message.Payload.ToArray(),
            Timestamp = message.Time,
            Headers = headers
        };
    }

    private static byte[] ToBinary(object? obj, Encoding encoding, Func<object, byte[]> converter)
    {
        switch (obj)
        {
            case null:
                return [];
            case byte b:
                return [b];
            case byte[] bArr:
                return bArr;
            case ReadOnlyMemory<byte> m:
                return m.ToArray();
            case Memory<byte> mArr:
                return mArr.ToArray();
            case sbyte sb:
                return [(byte)sb];
            case bool bo:
                return BitConverter.GetBytes(bo);
            case char c:
                return BitConverter.GetBytes(c);
            case char[] cArray:
                return encoding.GetBytes(cArray);
            case string s:
                return encoding.GetBytes(s);
            case short sh:
                return BitConverter.GetBytes(sh);
            case ushort ush:
                return BitConverter.GetBytes(ush);
            case int i:
                return BitConverter.GetBytes(i);
            case uint ui:
                return BitConverter.GetBytes(ui);
            case long l:
                return BitConverter.GetBytes(l);
            case ulong ul:
                return BitConverter.GetBytes(ul);
            case double d:
                return BitConverter.GetBytes(d);
            case float f:
                return BitConverter.GetBytes(f);
            case decimal de:
            {
                var bits = decimal.GetBits(de);
                var bytes = new byte[16];
                Buffer.BlockCopy(bits, 0, bytes, 0, 16);
                return bytes;
            }
#if NET8_0_OR_GREATER
            case DateOnly dateOnly:
                return encoding.GetBytes(dateOnly.ToString("O"));
            case TimeOnly timeOnly:
                return encoding.GetBytes(timeOnly.ToString("O"));
            case Half h:
                return BitConverter.GetBytes(h);
#endif
#if NET9_0_OR_GREATER
            case Int128 i128:
                return BitConverter.GetBytes(i128);
            case UInt128 ui128:
                return BitConverter.GetBytes(ui128);
#endif
            case DateTimeOffset dto:
                return encoding.GetBytes(dto.ToString("O"));
            case DateTime dt:
                return encoding.GetBytes(dt.ToUniversalTime().ToString("O"));
            case Guid guid:
                return guid.ToByteArray();
            case TimeSpan timeSpan:
                return encoding.GetBytes(timeSpan.ToString("c"));
            case Uri uri:
                return encoding.GetBytes(uri.ToString());
            default:
                return converter(obj);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await producer.FlushAsync();
        await producer.DisposeAsync();
    }
}
