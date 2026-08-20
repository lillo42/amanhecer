using System;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface ITransformer
{
    void Initialize(object? metadata);
    
    ValueTask EncodingAsync(Message message,
        IPipelineContext context,
        Func<Message, IPipelineContext, ValueTask> next);

    ValueTask DecodeAsync(Message message,
        IPipelineContext context,
        Func<Message, IPipelineContext, ValueTask> next);
}