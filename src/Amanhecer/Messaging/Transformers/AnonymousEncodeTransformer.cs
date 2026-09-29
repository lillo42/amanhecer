using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Transformers;

/// <summary>
/// An <see cref="IEncodeTransformer"/> that delegates execution to an
/// inline delegate, allowing anonymous encode transformers to be added to a pipeline.
/// </summary>
/// <param name="func">The delegate executed as the encode transformer body.</param>
public class AnonymousEncodeTransformer(Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func): IEncodeTransformer
{
    /// <inheritdoc />
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context, Func<Message, AmanhecerContext, ValueTask> next)
    {
        await func(message, context, next).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}