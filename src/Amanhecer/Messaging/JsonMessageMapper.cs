using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// A <see cref="MessageMapper{TRequest}"/> that serialises requests to and from JSON using
/// <see cref="JsonSerializer"/>.
/// </summary>
/// <typeparam name="TRequests">The application type mapped to and from messages.</typeparam>
/// <param name="options">The JSON serialiser options.</param>
public class JsonMessageMapper<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
                                DynamicallyAccessedMemberTypes.PublicMethods)]
    TRequests>(
    JsonSerializerOptions options) : MessageMapper<TRequests>
{
    /// <inheritdoc />
    public override ValueTask<Message> ToMessageAsync(TRequests request, AmanhecerContext context)
    {
        return new ValueTask<Message>(new Message
        {
            Id = context.RequestId,
            CorrelationId = context.CorrelationId,
#pragma warning disable IL2026
#pragma warning disable IL3050
            Payload = JsonSerializer.SerializeToUtf8Bytes(request, options).AsMemory()
#pragma warning restore IL3050
#pragma warning restore IL2026
        });
    }


    /// <inheritdoc />
    public override ValueTask<TRequests> ToRequestAsync(Message message, AmanhecerContext context)
    {
#pragma warning disable IL3050
#pragma warning disable IL2026
        return new ValueTask<TRequests>(JsonSerializer.Deserialize<TRequests>(message.Payload.Span, options)!);
#pragma warning restore IL2026
#pragma warning restore IL3050
    }
}

/// <summary>
/// An <see cref="IMessageMapper"/> that serialises requests to and from JSON using
/// <see cref="JsonSerializer"/>, without a compile-time request type: requests are serialised
/// using their runtime type, and messages are deserialised to the request type expected by the
/// pipeline handling them (see <see cref="MetadataName.RequestType"/>).
/// </summary>
public class JsonMessageMapper : IMessageMapper
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initialises the mapper with the default JSON serialiser options.
    /// </summary>
    public JsonMessageMapper() : this(new JsonSerializerOptions())
    {
    }

    /// <summary>
    /// Initialises the mapper with the given JSON serialiser options.
    /// </summary>
    /// <param name="options">The JSON serialiser options.</param>
    public JsonMessageMapper(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
    {
#pragma warning disable IL2026
#pragma warning disable IL3050
        return new ValueTask<Message>(new Message
        {
            Id = context.RequestId,
            CorrelationId = context.CorrelationId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(request, request.GetType(), _options).AsMemory()
        });
#pragma warning restore IL3050
#pragma warning restore IL2026
    }

    /// <inheritdoc />
    public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
    {
        var requestType = context.GetRequiredMetadata<Type>(MetadataName.RequestType);

#pragma warning disable IL2026
#pragma warning disable IL3050
        return new ValueTask<object>(JsonSerializer.Deserialize(message.Payload.Span, requestType, _options)!);
#pragma warning restore IL3050
#pragma warning restore IL2026
    }
}