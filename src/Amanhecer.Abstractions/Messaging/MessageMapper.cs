using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public abstract class MessageMapper<TRequest> : IMessageMapper<TRequest>
{
    public abstract ValueTask<Message> ToMessageAsync(TRequest request, IPipelineContext context);
    
    public abstract ValueTask<TRequest> ToRequestAsync(Message message, IPipelineContext context);

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