using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Metadatas;

namespace Amanhecer.Messaging.Middlewares;

/// <summary>
/// Middleware that decodes a consumed <see cref="Message"/> into the application request the
/// pipeline expects: it runs the decode transformer pipeline of the subscription stored in the
/// context metadata (<see cref="MetadataName.Subscription"/>) and maps the message with the
/// <see cref="IMessageMapper"/> configured on it.
/// </summary>
/// <param name="transformerPipelineFactory">The factory that builds the subscription's decode transformer pipeline.</param>
/// <param name="messageMapperFactory">The factory that resolves the subscription's message mapper.</param>
public class DecodeMiddleware(
    IDecodeTransformerPipelineFactory transformerPipelineFactory,
    IMessageMapperFactory messageMapperFactory) : IMiddleware
{
    /// <summary>
    /// Decodes the <see cref="Message"/> in <see cref="AmanhecerContext.Request"/> into the
    /// application request and continues the pipeline with it. Requests that are not a
    /// <see cref="Message"/>, or pipelines without a subscription in the metadata, pass through
    /// unchanged.
    /// </summary>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the subscription has no message mapper configured.
    /// </exception>
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        if (context.Request is not Message message)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var subscription = context.GetMetadata<ISubscription>(MetadataName.Subscription);
        if (subscription == null)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        var mapperType = context.GetMetadata<Type>(MetadataName.MessageMapperType);
        if (mapperType == null)
        {
            throw new InvalidOperationException(
                $"No message mapper is configured for subscription '{subscription.Name}'. " +
                $"Set {nameof(ISubscription)}.{nameof(ISubscription.MessageMapperType)} on the subscription " +
                "or configure a default message mapper.");
        }

        var transformerPipeline = TransformerPipelineNames.Decode(subscription.Name);
        var pipeline = transformerPipelineFactory.Create(transformerPipeline, context);
        await pipeline.DecodeAsync(message, context).ConfigureAwait(context.ContinueOnCapturedContext);

        // The mapper needs to know the request type expected by the pipeline's handler; it is
        // resolved from the handler type stored in the metadata when the pipeline was built,
        // unless the caller already set it explicitly.
        if (context.GetMetadata<Type>(MetadataName.RequestType) == null &&
            ResolveRequestType(context) is { } requestType)
        {
            context.SetMetadata(requestType, MetadataName.RequestType);
        }

        var mapper = messageMapperFactory.Create(mapperType);
        var request = await mapper.ToRequestAsync(message, context).ConfigureAwait(context.ContinueOnCapturedContext);

        context.SetMetadata(message);
        context.Request = request;

        await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    private static Type? ResolveRequestType(AmanhecerContext context)
    {
        var handlerType = context.GetMetadata<HandleTypeMetadata>()?.HandlerType;
        if (handlerType == null)
        {
            return null;
        }

        foreach (var @interface in handlerType.GetInterfaces())
        {
            if (!@interface.IsGenericType)
            {
                continue;
            }

            var genericTypeDefinition = @interface.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IRequestHandler<>) ||
                genericTypeDefinition == typeof(IQueryHandler<,>))
            {
                return @interface.GetGenericArguments()[0];
            }
        }

        return null;
    }
}