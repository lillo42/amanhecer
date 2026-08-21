using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IMessageMapper<TRequest> : IMessageMapper
{
    ValueTask<Message> ToMessageAsync(TRequest request, IPipelineContext context);

    new ValueTask<TRequest> ToRequestAsync(Message message, IPipelineContext context);

#if NET8_0_OR_GREATER
    async ValueTask<Message> IMessageMapper.ToMessageAsync(object request, IPipelineContext context)
        => await ToMessageAsync((TRequest)request, context);

    async ValueTask<object> IMessageMapper.ToRequestAsync(Message message, IPipelineContext context)
        => (await ToRequestAsync(message, context))!;
#endif
}

public interface IMessageMapper
{
    ValueTask<Message> ToMessageAsync(object request, IPipelineContext context);

    ValueTask<object> ToRequestAsync(Message message, IPipelineContext context);
}