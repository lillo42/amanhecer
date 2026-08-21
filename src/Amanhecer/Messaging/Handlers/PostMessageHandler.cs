using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Handlers;

public class PostMessageHandler(
    IPublicationFinder publicationFinder,
    IProducerFinder producerFinder,
    IMessageMapperFactory messageMapperFactory,
    ITransformerPipelineFactory transformerPipelineFactory) : IRequestHandler
{
    public async ValueTask HandleAsync(object request, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Metadata.TryGetValue(MetadataName.PublicationRoutingKey, out var obj))
        {
            // TODO: change exceptiopn
            throw new NotImplementedException();
        }

        if (obj is not string publicationRoutingKey)
        {
            throw new InvalidOperationException();
        }

        var publication = publicationFinder.Find(publicationRoutingKey);

        if (publication.MessageMapperType is null)
        {
            throw new InvalidOperationException(
                $"The publication '{publication.Name}' has no message mapper configured.");
        }

        var messageMapper = messageMapperFactory.Create(publication.MessageMapperType);

        context.Metadata[MetadataName.Publication] = publication;
        context.Metadata[MetadataName.PublicationMessageMapper] = messageMapper;

        var message = await ToMessageAsync(request, messageMapper, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var producer = producerFinder.Find(publicationRoutingKey);
        await producer.ProducerAsync(message, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private async ValueTask<Message> ToMessageAsync(object request,
        IMessageMapper messageMapper,
        IPublication publication,
        IPipelineContext context)
    {
        if (request is Message existingMessage)
        {
            return existingMessage;
        }

        var message = await messageMapper.ToMessageAsync(request, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var transformerPipelineName = $"Amanhecer.Messaging.Transformer.Encode.{publication.Name}";
        var pipeline = transformerPipelineFactory.Create(transformerPipelineName, context);
        await pipeline.EncodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        return message;
    }
}