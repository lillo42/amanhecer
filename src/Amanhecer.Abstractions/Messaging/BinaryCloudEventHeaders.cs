using System.Globalization;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Writes the CloudEvents attributes of a message as <c>cloudEvents:*</c> headers, the binary
/// content mode of the CloudEvents specification. Transports call it from their producers so
/// every transport exposes the attributes the same way.
/// </summary>
public static class BinaryCloudEventHeaders
{
    /// <summary>
    /// Sets the <c>cloudEvents:*</c> headers of the message from its attributes and from the
    /// extension attributes of the publication. Existing headers are never overwritten, and
    /// nothing is written when the publication uses <see cref="CloudEventType.Json"/>, because
    /// in structured content mode the attributes are members of the JSON envelope in the
    /// message body instead.
    /// </summary>
    /// <param name="message">The message whose headers are set.</param>
    /// <param name="publication">The publication the message is published through.</param>
    public static void Apply(Message message, IPublication publication)
    {
        if (publication.CloudEventType == CloudEventType.Json)
        {
            return;
        }

        Set(message, "cloudEvents:id", message.Id);
        Set(message, "cloudEvents:source", message.Source?.ToString());
        Set(message, "cloudEvents:specversion", message.SpecVersion);
        Set(message, "cloudEvents:type", message.Type);
        Set(message, "cloudEvents:datacontenttype", message.ContentType?.ToString());
        Set(message, "cloudEvents:dataschema", message.DataSchema?.ToString());
        Set(message, "cloudEvents:subject", message.Subject);
        Set(message, "cloudEvents:time", message.Time.ToString("O", CultureInfo.InvariantCulture));
        Set(message, "cloudEvents:baggage", message.Baggage?.ToString());
        Set(message, "cloudEvents:traceparent", message.TraceParent);
        Set(message, "cloudEvents:tracestate", message.TraceState?.ToString());

        // Extension attributes never overwrite the standard attributes set above.
        foreach (var additional in publication.AdditionalCloudEvents)
        {
            Set(message, $"cloudEvents:{additional.Key}", additional.Value);
        }
    }

    private static void Set(Message message, string key, object? value)
    {
        if (value == null)
        {
            return;
        }

        var headers = message.Headers;
#if NETFRAMEWORK || NETSTANDARD
        if (!headers.ContainsKey(key))
        {
            headers.Add(key, value);
        }
#else
        headers.TryAdd(key, value);
#endif
    }
}
