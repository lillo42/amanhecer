using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging.Handlers;

/// <summary>
/// The handler that publishes requests as messages: resolves the publication for the routing key
/// the message was posted with and sends the message through the producer resolved for that
/// routing key.
/// </summary>
/// <param name="producerFinder">The finder used to resolve the producer for the routing key.</param>
/// <param name="publicationFinder">The finder used to resolve the publication for the routing key.</param>
public class PostMessageHandler(IProducerFinder producerFinder, IPublicationFinder publicationFinder) : RequestHandler<Message>
{
    /// <inheritdoc />
    public override async ValueTask HandleAsync(Message request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        var publicationRoutingKey = context.GetRequiredMetadata<string>(MetadataName.PublicationRoutingKey);

        IPublication publication;
        try
        {
            publication = publicationFinder.Find(publicationRoutingKey);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"No publication is configured for the routing key '{publicationRoutingKey}'. " +
                "Declare a publication for the message type's routing key on a messaging gateway.",
                exception);
        }

        var producer = producerFinder.Find(publicationRoutingKey);
        await producer.ProduceAsync(request, publication, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
