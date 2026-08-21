using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;

namespace Amanhecer.Handlers;

/// <summary>
/// The handler that publishes requests as messages: maps the request to a <see cref="Message"/>,
/// runs the publication's encode transformer pipeline, and sends it through the producer resolved
/// for the publication routing key.
/// </summary>
/// <param name="publicationFinder">The finder used to resolve the publication for the routing key.</param>
/// <param name="producerFinder">The finder used to resolve the producer for the routing key.</param>
/// <param name="messageMapperFactory">The factory used to create the publication's message mapper.</param>
/// <param name="transformerPipelineFactory">The factory used to create the encode transformer pipeline.</param>
public class PostMessageHandler(
    IPublicationFinder publicationFinder,
    IProducerFinder producerFinder,
    IMessageMapperFactory messageMapperFactory,
    IEncodeTransformerPipelineFactory transformerPipelineFactory) : IRequestHandler
{
    /// <inheritdoc />
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

        var transformerPipelineName = TransformerPipelineNames.Encode(publication.Name);
        var pipeline = transformerPipelineFactory.Create(transformerPipelineName, context);
        await pipeline.EncodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        return message;
    }
}