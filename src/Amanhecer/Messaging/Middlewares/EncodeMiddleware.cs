using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Middlewares;

/// <summary>
/// The middleware that turns the request being posted into a <see cref="Message"/>: maps it
/// with the publication's <see cref="IMessageMapper"/>, runs it through the publication's
/// encode transformer pipeline, and replaces <see cref="AmanhecerContext.Request"/> with the
/// resulting message (the original request is kept under
/// <see cref="MetadataName.OriginalRequest"/>). Requests that are already a
/// <see cref="Message"/> pass through unchanged.
/// </summary>
/// <param name="transformerPipelineFactory">The factory used to create the encode transformer pipeline.</param>
/// <param name="messageMapperFactory">The factory used to resolve the message mapper.</param>
/// <param name="publicationFinder">The finder used to resolve the publication for the routing key.</param>
public class EncodeMiddleware(
    IEncodeTransformerPipelineFactory transformerPipelineFactory,
    IMessageMapperFactory messageMapperFactory,
    IPublicationFinder publicationFinder) : IMiddleware
{
    /// <inheritdoc />
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
            context.SetMetadata(publicationRoutingKey, MetadataName.PublicationRoutingKey);
        }

        publication = publicationFinder.Find(publicationRoutingKey!);
        context.SetMetadata(publication);

        return publication;
    }
}