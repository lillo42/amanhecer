using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;

namespace Amanhecer.Handlers;

/// <summary>
/// The handler that processes incoming messages: runs the subscription's decode transformer
/// pipeline over the message, maps it back to a request, and dispatches it to the local handlers.
/// </summary>
/// <param name="dispatcher">The dispatcher used to invoke the local handlers.</param>
/// <param name="messageMapperFactory">The factory used to create the subscription's message mapper.</param>
/// <param name="transformerPipelineFactory">The factory used to create the decode transformer pipeline.</param>
public class ReceiveMessageHandler(
    IDispatcher dispatcher,
    IMessageMapperFactory messageMapperFactory,
    IDecodeTransformerPipelineFactory transformerPipelineFactory
) : QueryHandler<Message, object?>
{
    /// <inheritdoc />
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
        var transformerPipelineName = TransformerPipelineNames.Decode(subscription.Name);

        var pipeline = transformerPipelineFactory.Create(transformerPipelineName, context);
        await pipeline.DecodeAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        return await messageMapper.ToRequestAsync(message, context)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }
}