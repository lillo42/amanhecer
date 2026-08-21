using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Maps between a strongly-typed application request and a <see cref="Message"/>.
/// </summary>
/// <typeparam name="TRequest">The application type mapped to and from messages.</typeparam>
public interface IMessageMapper<TRequest> : IMessageMapper
{
    /// <summary>
    /// Maps an application request to a <see cref="Message"/> ready to be published.
    /// </summary>
    /// <param name="request">The request to map.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The mapped message.</returns>
    ValueTask<Message> ToMessageAsync(TRequest request, IPipelineContext context);

    /// <summary>
    /// Maps a received <see cref="Message"/> back to an application request.
    /// </summary>
    /// <param name="message">The message to map.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The mapped request.</returns>
    new ValueTask<TRequest> ToRequestAsync(Message message, IPipelineContext context);

#if NET8_0_OR_GREATER
    async ValueTask<Message> IMessageMapper.ToMessageAsync(object request, IPipelineContext context)
        => await ToMessageAsync((TRequest)request, context);

    async ValueTask<object> IMessageMapper.ToRequestAsync(Message message, IPipelineContext context)
        => (await ToRequestAsync(message, context))!;
#endif
}

/// <summary>
/// Non-generic view of a message mapper, used when the mapped type is only known at runtime.
/// </summary>
public interface IMessageMapper
{
    /// <summary>
    /// Maps an application request to a <see cref="Message"/> ready to be published.
    /// </summary>
    /// <param name="request">The request to map.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The mapped message.</returns>
    ValueTask<Message> ToMessageAsync(object request, IPipelineContext context);

    /// <summary>
    /// Maps a received <see cref="Message"/> back to an application request.
    /// </summary>
    /// <param name="message">The message to map.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The mapped request.</returns>
    ValueTask<object> ToRequestAsync(Message message, IPipelineContext context);
}