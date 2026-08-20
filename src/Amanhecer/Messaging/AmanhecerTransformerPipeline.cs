using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

public class AmanhecerTransformerPipeline : ITransformerPipeline
{
    private readonly Func<Message, IPipelineContext, ValueTask> _decodeChain;

    public AmanhecerTransformerPipeline(IReadOnlyList<ITransformer> transformers)
    {
        Func<Message, IPipelineContext, ValueTask> next = static (_, _) => new ValueTask();

        for (var i = transformers.Count; i >= 0; i--)
        {
            var transformer = transformers[i];
            var continuation = next;
            next = (message, context) => transformer.DecodeAsync(message, context, continuation);
        }

        _decodeChain = next;
    }
    
    public async ValueTask DecodeAsync(Message message, IPipelineContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        await _decodeChain(message, context);
    }
}