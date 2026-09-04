using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Middlewares;

public class DecodeMiddleware(
    IDecodeTransformerPipelineFactory transformerPipelineFactory,
    IMessageMapperFactory messageMapperFactory) : IMiddleware
{
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        if (context.Request is not Message message)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var subscription = context.GetMetadata<ISubscription>(MetadataName.Subscription);
        if (subscription == null)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var mapperType = context.GetMetadata<Type>(MetadataName.MessageMapperType);
        if (mapperType == null)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var transformerPipeline = TransformerPipelineNames.Decode(subscription.Name);
        var pipeline = transformerPipelineFactory.Create(transformerPipeline, context);
        await pipeline.DecodeAsync(message, context).ConfigureAwait(context.ContinueOnCapturedContext);

        var mapper = messageMapperFactory.Create(mapperType);
        var request = await mapper.ToRequestAsync(message, context).ConfigureAwait(context.ContinueOnCapturedContext);

        context.SetMetadata(message);
        context.Request = request;

        await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}