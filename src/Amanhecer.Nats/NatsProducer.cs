using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Net;

namespace Amanhecer.Nats;

public class NatsProducer(NatsClient client) : IProducer
{
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context) 
    {
        if (publication is not NatsPublication natsPublication)
        {
            throw new NotImplementedException();
        }

        await client.PublishAsync(natsPublication.Subject,
                message.Payload.ToArray(),
                ToNatsHeaders(message, natsPublication),
                message.ReplyTo,
                natsPublication.Serializer,
                cancellationToken: context.CancellationToken)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private static NatsHeaders ToNatsHeaders(Message message, NatsPublication publication)
    {
        var headers = new NatsHeaders(publication.CaseSensitive);

        foreach (var header in message.Headers)
        {
            if(header.Value != null)
            {
                headers[header.Key] = publication.ConverterToString(header.Value);
            }
        }

        return headers;
    }
}