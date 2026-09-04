using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Options;
using Amanhecer.Messaging.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging;

/// <summary>
/// 
/// </summary>
public class AmanhecerPumper(IServiceProvider provider, ILogger<AmanhecerPumper> logger) : IMessagePumper
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(IConsumer consumer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var subscription = consumer.Subscription;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (subscription.ReceiveMessageTimeout != Timeout.InfiniteTimeSpan)
                {
                    cts.CancelAfter(subscription.ReceiveMessageTimeout);
                }

                var messages = await ReceiveMessagesAsync(consumer, cancellationToken);
                if (messages.Length == 0 && subscription.NoMessageDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.NoMessageDelay, cancellationToken);
                }

                await ProcessesAsync(consumer, messages, cancellationToken);
            }
            catch (Exception e)
            {
                if (subscription.FailureDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.FailureDelay, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessesAsync(IConsumer consumer, IEnumerable<Message> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            var subscription = GetSubscription(message);

            var gateway = message.Metadata[MetadataName.MessagingGateway];
            var activity = AmanhecerDiagnostics.ActivitySource.CreateActivity(
                "",
                ActivityKind.Consumer,
                parentId: message.TraceParent);

            await using var scope = provider.CreateAsyncScope();
            var serviceProvider = scope.ServiceProvider;
            var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

            try
            {
                var action = await ProcessAsync(message,
                        subscription,
                        dispatcher,
                        activity,
                        [],
                        cancellationToken)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                await ApplyActionAsync(message, action, consumer, subscription, dispatcher)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (DeferException deferException)
            {
                await consumer.DeferAsync(message, deferException.Delay)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (NackException)
            {
                await consumer.NackAsync(message)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception e)
            {
                await ApplyActionAsync(message,
                        subscription.OnError(message, e),
                        consumer,
                        subscription,
                        dispatcher)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);
#if !NET8_0
                activity?.AddException(e);
#endif
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            finally
            {
                activity?.Stop();
            }
        }
    }

    private static async Task<IConsumerAction> ProcessAsync(Message message,
        ISubscription subscription,
        IDispatcher dispatcher,
        Activity? activity,
        List<KeyValuePair<string, object?>> telemetryTags,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync<object?>(message, new AmanhecerContext
            {
                Activity = activity,
                ContinueOnCapturedContext = subscription.ContinueOnCapturedContext,
                CorrelationId = message.CorrelationId,
                Metadata = new Dictionary<string, object?>
                {
                    [MetadataName.Message] = message,
                    [MetadataName.Subscription] = subscription,
                    [MetadataName.MessageMapperType] = subscription.MessageMapperType
                },
                Middlewares = [new AmanhecerMiddlewareOptions(typeof(DecodeMiddleware), 0, null)],
                RequestId = message.Id,
                RoutingKey = subscription.ToRoutingKey,
                TelemetryTags = telemetryTags,
            }, cancellationToken)
            .ConfigureAwait(subscription.ContinueOnCapturedContext);

        if (result is IResolvingConsumerAction resolver)
        {
            return await resolver.ExecuteAsync(message, subscription, dispatcher, cancellationToken)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);
        }

        if (result is IConsumerAction action)
        {
            return action;
        }

        return Ack.Instance;
    }

    private static async ValueTask ApplyActionAsync(
        Message message,
        IConsumerAction action,
        IConsumer consumer,
        ISubscription subscription,
        IDispatcher dispatcher)
    {
        if (action is IResolvingConsumerAction resolver)
        {
            action = await resolver.ExecuteAsync(message, subscription, dispatcher)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);
        }

        if (action is Ack)
        {
            await consumer.AckAsync(message)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);
        }
        else if (action is Nack)
        {
            await consumer.NackAsync(message)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);
        }
        else if (action is Defer defer)
        {
            await consumer.DeferAsync(message, defer.Delay)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);
        }
    }

    private static ISubscription GetSubscription(Message message)
    {
        if (message.Headers.TryGetValue(MetadataName.Subscription, out var obj) && obj is ISubscription subscription)
        {
            return subscription;
        }

        throw new InvalidOperationException();
    }

    private static async ValueTask<Message[]> ReceiveMessagesAsync(IConsumer consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await consumer.GetMessagesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }
}