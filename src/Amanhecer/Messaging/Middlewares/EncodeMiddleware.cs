using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Middlewares;

public class EncodeMiddleware(
    IEncodeTransformerPipelineFactory transformerPipelineFactory,
    IMessageMapperFactory messageMapperFactory,
    IPublicationFinder publicationFinder) : IMiddleware
{
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        if (context.Request is Message)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var publication = ResolvePublication(context);

        var mapperType = context.GetMetadata<Type>(MetadataName.MessageMapperType) ?? publication.MessageMapperType!;
        var mapper = messageMapperFactory.Create(mapperType);
        var message = await mapper.ToMessageAsync(context.Request, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var transformerPipeline = TransformerPipelineNames.Encode(publication.Name);
        var pipeline = transformerPipelineFactory.Create(transformerPipeline, context);
        await pipeline.EncodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        context.SetMetadata(context.Request, MetadataName.OriginalRequest);
        context.Request = message;

        await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private IPublication ResolvePublication(AmanhecerContext context)
    {
        var publication = context.GetMetadata<IPublication>(MetadataName.Publication);
        if (publication != null)
        {
            return publication;
        }

        var publicationRoutingKey = context.GetMetadata<string?>(MetadataName.PublicationRoutingKey);
        if (string.IsNullOrEmpty(publicationRoutingKey))
        {
            publicationRoutingKey = context.Request.GetType().FullName ?? context.Request.GetType().Name;
            context.SetMetadata(MetadataName.PublicationRoutingKey, publicationRoutingKey);
        }

        publication = publicationFinder.Find(publicationRoutingKey!);
        context.SetMetadata(publication);

        return publication;
    }
}