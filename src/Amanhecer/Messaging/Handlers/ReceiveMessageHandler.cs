using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Handlers;

public class ReceiveMessageHandler(
    IDispatcher dispatcher,
    IMessageMapperFactory messageMapperFactory,
    ITransformerPipelineFactory transformerPipelineFactory
) : QueryHandler<Message, object?>
{
    public override async ValueTask<object?> HandleAsync(Message query, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var subscription = GetSubscription(context);
        var messageMapper = messageMapperFactory.Create(subscription.MessageMapperType);

        context.Metadata[MetadataName.SubscriptionMessageMapper] = messageMapper;

        var request = await ToRequestAsync(query, messageMapper, subscription, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        return await dispatcher.QueryAsync(request,
                new AmanhecerContext
                {
                    RoutingKey = subscription.ToRoutingKey,
                    ContinueOnCapturedContext = context.ContinueOnCapturedContext,
                    CorrelationId = query.CorrelationId,
                    RequestId = query.Id,
                    Metadata = context.Metadata
                },
                cancellationToken)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private static ISubscription GetSubscription(IPipelineContext context)
    {
        var subscription = context.Metadata.GetOrDefault<ISubscription>(MetadataName.Subscription);
        if (subscription == null)
        {
            throw new NotImplementedException();
        }

        return subscription;
    }

    private async ValueTask<object> ToRequestAsync(Message message,
        IMessageMapper messageMapper,
        ISubscription subscription,
        IPipelineContext context)
    {
        var transformerPipelineName = $"Amanhecer.Messaging.Transformer.Decode.{subscription.Name}";

        var pipeline = transformerPipelineFactory.Create(transformerPipelineName, context);
        await pipeline.DecodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        return await messageMapper.ToRequestAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }
}