using System;
using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;

namespace Amanhecer.Nats;

public class NatsPublication(string subject) : Publication
{
    public string Subject { get; set; } = subject;

    public bool CaseSensitive { get; set; }

    public INatsSerializer<byte[]>? Serializer { get; set; }

    public Func<object, StringValues> ConverterToString { get; set; } = ConverterFunc;

    /// <inheritdoc cref="IPublication.CloudEventType"/>
    public override CloudEventType CloudEventType { get; set; } = CloudEventType.Json;

    private static StringValues ConverterFunc(object obj)
    {
        if (obj is string s)
        {
            return s;
        }

        if (obj is char c)
        {
            return c.ToString();
        }

        if (obj is IEnumerable<string> values)
        {
            return new StringValues([.. values]);
        }

        if (obj is DateTime dateTime)
        {
            return dateTime.ToString("O");
        }

        if (obj is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("O");
        }
        
        if (obj is Guid guid)
        {
            return guid.ToString();
        }

        if (obj is TimeSpan timeSpan)
        {
            return timeSpan.ToString();
        }

        if (obj is Uri uri)
        {
            return uri.ToString();
        }

#if NET8_0_OR_GREATER
        if (obj is DateOnly dateOnly)
        {
            return dateOnly.ToString("O");
        }

        if (obj is TimeOnly timeOnly)
        {
            return timeOnly.ToString("O");
        }
#endif

        return obj.ToString();
    }
}