using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Transformers;

/// <summary>
/// An <see cref="ITransformer"/> that wraps outgoing messages into a CloudEvents structured
/// (JSON) envelope when the resolved <see cref="IPublication"/> uses
/// <see cref="CloudEventType.Json"/>, and unwraps incoming messages carrying the
/// <see cref="MediaType"/> content type.
/// </summary>
public class StructuredCloudEventTransformer : ITransformer
{
    /// <summary>
    /// The content type messages carrying a structured CloudEvents envelope are published with.
    /// </summary>
    public const string MediaType = "application/cloudevents+json";

    private static readonly HashSet<string> StandardEnvelopeAttributes = new(StringComparer.Ordinal)
    {
        "specversion", "id", "source", "type", "time", "datacontenttype", "dataschema",
        "subject", "baggage", "traceparent", "tracestate", "data", "data_base64", "dataref"
    };

    /// <inheritdoc />
    public ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var publication = context.GetMetadata<IPublication>(MetadataName.Publication);
        if (publication?.CloudEventType != CloudEventType.Json)
        {
            return next(message, context);
        }

        if (message.Source == Message.DefaultSource && publication.DefaultSource != null)
        {
            message.Source = publication.DefaultSource;
        }

        if (message.SpecVersion == Message.DefaultSpecVersion && publication.DefaultSpecVersion != null)
        {
            message.SpecVersion = publication.DefaultSpecVersion;
        }

        if (message.Type == Message.DefaultType && publication.DefaultType != null)
        {
            message.Type = publication.DefaultType;
        }
        if (string.IsNullOrEmpty(message.Id))
        {
            message.Id = context.RequestId;
        }

        message.Payload = SerializeEnvelope(message, publication);
        message.ContentType = new ContentType(MediaType);

        return next(message, context);
    }

    /// <inheritdoc />
    public ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var subscription = context.GetRequiredMetadata<ISubscription>(MetadataName.Subscription);
        if (subscription.CloudEventType == CloudEventType.Json  && IsStructuredCloudEvent(message.ContentType))
        {
            ApplyEnvelope(message);
        }

        return next(message, context);
    }

    private static bool IsStructuredCloudEvent(ContentType? contentType)
    {
        return contentType != null
            && (string.Equals(contentType.MediaType, MediaType, StringComparison.OrdinalIgnoreCase)
            || contentType.MediaType.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    // Structured content mode: the event is a single JSON object carrying the attributes
    // plus the data, published with the application/cloudevents+json content type.
    private static byte[] SerializeEnvelope(Message message, IPublication publication)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteString(writer, "specversion", message.SpecVersion);
            WriteString(writer, "id", message.Id);
            WriteString(writer, "source", message.Source?.ToString());
            WriteString(writer, "type", message.Type);
            writer.WriteString("time", message.Time.ToString("O", CultureInfo.InvariantCulture));
            WriteString(writer, "datacontenttype", message.ContentType?.ToString());
            WriteString(writer, "dataschema", message.DataSchema?.ToString());
            WriteString(writer, "subject", message.Subject);
            WriteString(writer, "baggage", message.Baggage?.ToString());
            WriteString(writer, "traceparent", message.TraceParent);
            WriteString(writer, "tracestate", message.TraceState?.ToString());

            // Extension attributes never overwrite the standard attributes written above.
            foreach (var additional in publication.AdditionalCloudEvents)
            {
                if (StandardEnvelopeAttributes.Contains(additional.Key))
                {
                    continue;
                }

                writer.WritePropertyName(additional.Key);
                WriteValue(writer, additional.Value);
            }

            if (!TryWriteJsonData(writer, message))
            {
                writer.WriteBase64String("data_base64", message.Payload.Span);
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        return stream.ToArray();

        static void WriteString(Utf8JsonWriter writer, string name, string? value)
        {
            if (value != null)
            {
                writer.WriteString(name, value);
            }
        }

        static void WriteValue(Utf8JsonWriter writer, object value)
        {
            switch (value)
            {
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case bool flag:
                    writer.WriteBooleanValue(flag);
                    break;
                case byte or sbyte or short or ushort or int or uint or long:
                    writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case ulong unsigned:
                    writer.WriteNumberValue(unsigned);
                    break;
                case float single:
                    writer.WriteNumberValue(single);
                    break;
                case double precision:
                    writer.WriteNumberValue(precision);
                    break;
                case decimal money:
                    writer.WriteNumberValue(money);
                    break;
                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }

    // A JSON payload is embedded as the raw `data` member; anything else is base64-encoded
    // into `data_base64`, per the CloudEvents JSON event format.
    private static bool TryWriteJsonData(Utf8JsonWriter writer, Message message)
    {
        if (message.Payload.IsEmpty || !IsJsonContentType(message.ContentType))
        {
            return false;
        }

        try
        {
            using (JsonDocument.Parse(message.Payload))
            {
            }
        }
        catch (JsonException)
        {
            return false;
        }

        writer.WritePropertyName("data");
        writer.WriteRawValue(message.Payload.Span, skipInputValidation: true);
        return true;
    }

    private static bool IsJsonContentType(ContentType? contentType)
    {
        var mediaType = contentType?.MediaType;
        return mediaType != null
            && (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    // Structured content mode: the payload is a single JSON object carrying the event
    // attributes plus the data. Unknown top-level members are CloudEvents extension
    // attributes, surfaced as cloudEvents:* headers like in binary content mode.
    private static void ApplyEnvelope(Message message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var envelope = document.RootElement;
        if (envelope.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The structured CloudEvents envelope must be a JSON object.");
        }

        var hasDataContentType = false;
        foreach (var attribute in envelope.EnumerateObject())
        {
            var value = attribute.Value.ValueKind == JsonValueKind.String
                ? attribute.Value.GetString()
                : null;

            switch (attribute.Name)
            {
                case "specversion":
                    if (!string.IsNullOrEmpty(value))
                    {
                        message.SpecVersion = value;
                    }

                    break;
                case "id":
                    if (!string.IsNullOrEmpty(value))
                    {
                        message.Id = value!;
                    }

                    break;
                case "source":
                    if (value != null && Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var source))
                    {
                        message.Source = source;
                    }

                    break;
                case "type":
                    if (!string.IsNullOrEmpty(value))
                    {
                        message.Type = value;
                    }

                    break;
                case "time":
                    if (value != null && DateTimeOffset.TryParse(value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var time))
                    {
                        message.Time = time;
                    }

                    break;
                case "datacontenttype":
                    hasDataContentType = true;
                    if (value != null)
                    {
                        message.ContentType = new ContentType(value);
                    }

                    break;
                case "dataschema":
                    if (value != null && Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var dataSchema))
                    {
                        message.DataSchema = dataSchema;
                    }

                    break;
                case "subject":
                    message.Subject = value;
                    break;
                case "baggage":
                    message.Baggage = value != null ? Baggage.FromString(value) : null;
                    break;
                case "traceparent":
                    message.TraceParent = value;
                    break;
                case "tracestate":
                    message.TraceState = value != null ? TraceState.FromString(value) : null;
                    break;
                case "dataref":
                    message.DataRef = value;
                    break;
                case "data":
                    message.Payload = Encoding.UTF8.GetBytes(attribute.Value.GetRawText());
                    break;
                case "data_base64":
                    if (attribute.Value.ValueKind == JsonValueKind.String)
                    {
                        message.Payload = attribute.Value.GetBytesFromBase64();
                    }

                    break;
                default:
                    message.Headers[$"cloudEvents:{attribute.Name}"] = GetExtensionValue(attribute.Value);
                    break;
            }
        }

        // The envelope content type describes the envelope, not the data: without an explicit
        // datacontenttype the payload content type is unknown.
        if (!hasDataContentType)
        {
            message.ContentType = null;
        }
    }

    private static object? GetExtensionValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
