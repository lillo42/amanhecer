using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Handlers;

public class PostMessageHandler(IPublicationFinder publicationFinder, 
    IProducerFinder producerFinder,
    ITransformerPipelineFactory transformerPipelineFactory) : IRequestHandler
{
    public async ValueTask HandleAsync(object request, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Metadata.TryGetValue(MetadataName.PublicationRoutingKey, out var obj))
        {
            // TODO: change exceptiopn
            throw new Exception();
        }

        if (obj is not string publicationRoutingKey)
        {
            throw new InvalidOperationException();
        }

        var publication = publicationFinder.Find(publicationRoutingKey);
        context.Metadata[MetadataName.Publication] = publication;
        context.Metadata[MetadataName.MessageMapper] = publication.MessageMapper;

        var message = await ToMessageAsync(request, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var producer = producerFinder.Find(publicationRoutingKey);
        await producer.ProducerAsync(message, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private async ValueTask<Message> ToMessageAsync(object request, 
        IPublication publication,
        IPipelineContext context)
    {
        if (request is Message existingMessage)
        {
            return existingMessage;
        }

        var message = await publication.MessageMapper.ToMessageAsync(request, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var pipeline = transformerPipelineFactory.Create(context);
        await pipeline.DecodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
        
        return message;
    }
}