using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

public class AmanhecerEncodeTransformerPipeline(IReadOnlyList<IEncodeTransformer> transformers)
    : IEncodeTransformerPipeline
{
    public async ValueTask EncodeAsync(Message message, IPipelineContext context)
    {
        Func<Message, IPipelineContext, ValueTask> next = static (_, _) => new ValueTask();
        for (var i = transformers.Count - 1; i >= 0; i--)
        {
            var transformer = transformers[i];
            var continuation = next;
            next = (m, c) => transformer.EncodeAsync(m, c, continuation);
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        await next(message, context);
    }
}
