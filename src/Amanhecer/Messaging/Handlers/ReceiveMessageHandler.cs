using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.Messaging;
using Amanhecer.Middlewares;

namespace Amanhecer.Handlers;

/// <summary>
/// The handler that processes incoming messages: runs the subscription's decode transformer
/// pipeline over the message, maps it back to a request, and dispatches it to the local handlers.
/// </summary>
/// <param name="dispatcher">The dispatcher used to invoke the local handlers.</param>
/// <param name="messageMapperFactory">The factory used to create the subscription's message mapper.</param>
/// <param name="transformerPipelineFactory">The factory used to create the decode transformer pipeline.</param>
/// <param name="pipelineOptions">The routing pipeline options, used to resolve the request type
/// expected by the pipeline the message is dispatched to.</param>
public class ReceiveMessageHandler(
    IDispatcher dispatcher,
    IMessageMapperFactory messageMapperFactory,
    IDecodeTransformerPipelineFactory transformerPipelineFactory,
    AmanhecerPipelineOptions pipelineOptions
) : QueryHandler<Message, object?>
{
    /// <inheritdoc />
    public override async ValueTask<object?> HandleAsync(Message query, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var subscription = GetSubscription(context);

        try
        {

            if (subscription.MessageMapperType is null)
            {
                throw new InvalidOperationException(
                    $"The subscription '{subscription.Name}' has no message mapper configured. " +
                    "Call MessageMapper<TMapper> on the subscription or DefaultMessageMapper when configuring the gateway.");
            }

            var messageMapper = messageMapperFactory.Create(subscription.MessageMapperType);

            context.Metadata[MetadataName.SubscriptionMessageMapper] = messageMapper;

            var requestType = GetRequestType(subscription.ToRoutingKey);
            if (requestType is not null)
            {
                context.Metadata[MetadataName.RequestType] = requestType;
            }

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
        catch(Exception exception)
        {
            var action = subscription.OnError(query, exception);
            if (action is IResolvingConsumerAction resolvingConsumerAction)
            {
                return await resolvingConsumerAction.ExecuteAsync(query, subscription, dispatcher, cancellationToken)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }
            
            return action;
        }
    }

    private Type? GetRequestType(string routingKey)
    {
        if (!pipelineOptions.Configuration.TryGetValue(routingKey, out var chains))
        {
            return null;
        }

        foreach (var chain in chains)
        {
            foreach (var middleware in chain)
            {
                if (middleware.MiddlewareType != typeof(ExecuteHandlerMiddleware) ||
                    middleware.Metadata is not Type handlerType)
                {
                    continue;
                }

                foreach (var @interface in handlerType.GetInterfaces())
                {
                    if (!@interface.IsGenericType)
                    {
                        continue;
                    }

                    var definition = @interface.GetGenericTypeDefinition();
                    if (definition == typeof(IRequestHandler<>) || definition == typeof(IQueryHandler<,>))
                    {
                        return @interface.GetGenericArguments()[0];
                    }
                }
            }
        }

        return null;
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