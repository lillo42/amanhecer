using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Transformers;

/// <summary>
/// An <see cref="IDecodeTransformer"/> that delegates execution to an
/// inline delegate, allowing anonymous decode transformers to be added to a pipeline.
/// </summary>
/// <param name="func">The delegate executed as the decode transformer body.</param>
public class AnonymousDecodeTransformer(Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func): IDecodeTransformer
{
    /// <inheritdoc />
    public async ValueTask DecodeAsync(Message message, AmanhecerContext context, Func<Message, AmanhecerContext, ValueTask> next)
    {
        await func(message, context, next).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}