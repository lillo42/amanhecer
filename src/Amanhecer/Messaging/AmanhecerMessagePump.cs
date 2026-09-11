using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
/// The default <see cref="IMessagePump"/>: polls an <see cref="IConsumer"/> for messages,
/// dispatches each one through the pipeline of the consumer's subscription and settles it
/// (ack, nack or defer) according to the result, until cancellation is requested.
/// </summary>
/// <param name="provider">The service provider used to create a scope per received batch.</param>
/// <param name="logger">The logger used to record pump failures.</param>
public partial class AmanhecerMessagePump(IServiceProvider provider, ILogger<AmanhecerMessagePump> logger) : IMessagePump
{
    /// <summary>Counts consumed messages whose processing settled successfully.</summary>
    private static readonly Counter<int> ConsumerSuccessCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.process.success",
        unit: "{message}",
        description: "Number of consumed messages whose processing settled successfully.");

    /// <summary>Counts consumed messages whose processing failed with an exception.</summary>
    private static readonly Counter<int> ConsumerFailedCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.process.failed",
        unit: "{message}",
        description: "Number of consumed messages whose processing failed with an exception.");

    /// <summary>Records how long processing a consumed message took, in seconds.</summary>
    private static readonly Histogram<double> ConsumerDuration = AmanhecerDiagnostics.Meter.CreateHistogram<double>(
        "amanhecer.message.process.duration",
        unit: "s",
        description: "Duration of consumed message processing, in seconds.");

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

                var messages = await ReceiveMessagesAsync(consumer, cts.Token);
                if (messages.Length == 0 && subscription.NoMessageDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.NoMessageDelay, cancellationToken);
                }

                await ProcessesAsync(consumer, subscription, messages, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (subscription.NoMessageDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.NoMessageDelay, cancellationToken);
                }

                break;
            }
            catch (Exception e)
            {
                Logger.PumpFailed(logger, subscription.Name, e);

                if (subscription.FailureDelay != TimeSpan.Zero)
                {
                    await Task.Delay(subscription.FailureDelay, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessesAsync(IConsumer consumer,
        ISubscription subscription,
        IEnumerable<Message> messages,
        CancellationToken cancellationToken)
    {
        // Low-cardinality tags shared by the metrics instruments (OTel messaging conventions).
        var metricTags = new List<KeyValuePair<string, object?>>
        {
            new("messaging.system", subscription.MessagingSystem),
            new("messaging.operation.type", "process"),
            new("messaging.destination.name", subscription.Name),
        }.ToArray();

        foreach (var message in messages)
        {
            var parentContext = ActivityContext.TryParse(message.TraceParent, message.TraceState?.ToString(),
                out var parsed)
                ? parsed
                : default;

            // Per-message values are high-cardinality, so they go on the span only, never on metrics.
            var spanTags = new List<KeyValuePair<string, object?>>(metricTags)
            {
                new("messaging.message.id", message.Id),
                new("messaging.message.conversation_id", message.CorrelationId),
            };

            var activity = AmanhecerDiagnostics.ActivitySource.StartActivity(
                $"{subscription.Name} process",
                ActivityKind.Consumer,
                parentContext,
                tags: spanTags);

            var duration = Stopwatch.StartNew();

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
                ConsumerSuccessCounter.Add(1, metricTags);
            }
            catch (DeferException deferException)
            {
                await consumer.DeferAsync(message, deferException.Delay)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                activity?.SetStatus(ActivityStatusCode.Ok);
                ConsumerSuccessCounter.Add(1, metricTags);
            }
            catch (NackException)
            {
                await consumer.NackAsync(message)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);

                activity?.SetStatus(ActivityStatusCode.Ok);
                ConsumerSuccessCounter.Add(1, metricTags);
            }
            catch (Exception e)
            {
                try
                {
                    await ApplyActionAsync(message,
                            subscription.OnError(message, e),
                            consumer,
                            subscription,
                            dispatcher)
                        .ConfigureAwait(subscription.ContinueOnCapturedContext);
                }
                catch (Exception errorActionException)
                {
                    // The error action itself failed; fall back to a nack so the message is
                    // always settled rather than left unacked until the channel closes.
                    Logger.ErrorActionFailed(logger, subscription.Name, errorActionException);
                    await consumer.NackAsync(message)
                        .ConfigureAwait(subscription.ContinueOnCapturedContext);
                }
#if !NET8_0
                activity?.AddException(e);
#endif
                activity?.SetStatus(ActivityStatusCode.Error);
                ConsumerFailedCounter.Add(1, metricTags);
            }
            finally
            {
                duration.Stop();
                ConsumerDuration.Record(duration.Elapsed.TotalSeconds, metricTags);
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
                    [MetadataName.OriginalMessage] = message,
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

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Error,
            "An error occurred while receiving messages from subscription {SubscriptionName}; retrying after the failure delay.")]
        public static partial void PumpFailed(ILogger logger, string subscriptionName, Exception exception);

        [LoggerMessage(LogLevel.Error,
            "The error action of subscription {SubscriptionName} failed; the message is nacked instead.")]
        public static partial void ErrorActionFailed(ILogger logger, string subscriptionName, Exception exception);
    }
}