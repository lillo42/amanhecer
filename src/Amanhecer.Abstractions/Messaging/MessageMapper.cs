using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Base class for message mappers: maps between a strongly-typed application request
/// and a <see cref="Message"/>, implementing the non-generic <see cref="IMessageMapper"/>
/// members on top of the strongly-typed ones.
/// </summary>
/// <typeparam name="TRequest">The application type mapped to and from messages.</typeparam>
public abstract class MessageMapper<TRequest> : IMessageMapper<TRequest>
{
    /// <inheritdoc cref="IMessageMapper{TRequest}.ToMessageAsync(TRequest, IPipelineContext)"/>
    public abstract ValueTask<Message> ToMessageAsync(TRequest request, IPipelineContext context);

    /// <inheritdoc cref="IMessageMapper{TRequest}.ToRequestAsync(Message, IPipelineContext)"/>
    public abstract ValueTask<TRequest> ToRequestAsync(Message message, IPipelineContext context);

    /// <inheritdoc cref="IMessageMapper.ToMessageAsync"/>
    public virtual async ValueTask<Message> ToMessageAsync(object request, IPipelineContext context)
    {
       return await ToMessageAsync((TRequest)request, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    async ValueTask<object> IMessageMapper.ToRequestAsync(Message message, IPipelineContext context)
    {
        return (await ToRequestAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext))!;
    }
}