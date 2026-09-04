using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IDecodeTransformerPipeline"/> that runs an ordered chain of decode transformers,
/// each transformer invoking the next one in the chain.
/// </summary>
/// <param name="transformers">The ordered transformers that compose the pipeline.</param>
public class AmanhecerDecodeTransformerPipeline(IReadOnlyList<IDecodeTransformer> transformers)
    : IDecodeTransformerPipeline
{
    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context)
    {
        Func<Message, AmanhecerContext, ValueTask> next = static (_, _) => new ValueTask();
        for (var i = transformers.Count - 1; i >= 0; i--)
        {
            var transformer = transformers[i];
            var continuation = next;
            next = (m, c) => transformer.DecodeAsync(m, c, continuation);
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        await next(message, context);
    }
}
