using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
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
    public override ValueTask<Message> ToMessageAsync(TRequests request, IPipelineContext context)
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
    public override ValueTask<TRequests> ToRequestAsync(Message message, IPipelineContext context)
    {
#pragma warning disable IL3050
#pragma warning disable IL2026
        return new ValueTask<TRequests>(JsonSerializer.Deserialize<TRequests>(message.Payload.Span, options)!);
#pragma warning restore IL2026
#pragma warning restore IL3050
    }
}