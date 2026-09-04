using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Handlers;

/// <summary>
/// The handler that publishes requests as messages: maps the request to a <see cref="Message"/>,
/// runs the publication's encode transformer pipeline, and sends it through the producer resolved
/// for the publication routing key.
/// </summary>
/// <param name="producerFinder">The finder used to resolve the producer for the routing key.</param>
public class PostMessageHandler(IProducerFinder producerFinder) : RequestHandler<Message> 
{
    /// <inheritdoc />
    public override async ValueTask HandleAsync(Message request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        var publicationRoutingKey = context.GetRequiredMetadata<string>(MetadataName.PublicationRoutingKey);
        var publication = context.GetRequiredMetadata<IPublication>(MetadataName.Publication);
        
        var producer = producerFinder.Find(publicationRoutingKey);
        await producer.ProducerAsync(request, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }
}