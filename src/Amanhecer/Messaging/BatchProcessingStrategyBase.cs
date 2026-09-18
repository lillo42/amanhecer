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
/// Provides shared message-processing behavior for batch processing strategies.
/// </summary>
public abstract partial class BatchProcessingStrategyBase
{
    /// <summary>
    /// Counts consumed messages whose processing settled successfully.
    /// </summary>
    private static readonly Counter<int> ConsumerSuccessCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.process.success",
        unit: "{message}",
        description: "Number of consumed messages whose processing settled successfully.");

    /// <summary>
    /// Counts consumed messages whose processing failed with an exception.
    /// </summary>
    private static readonly Counter<int> ConsumerFailedCounter = AmanhecerDiagnostics.Meter.CreateCounter<int>(
        "amanhecer.message.process.failed",
        unit: "{message}",
        description: "Number of consumed messages whose processing failed with an exception.");

    /// <summary>
    /// Records how long processing a consumed message took, in seconds.
    /// </summary>
    private static readonly Histogram<double> ConsumerDuration = AmanhecerDiagnostics.Meter.CreateHistogram<double>(
        "amanhecer.message.process.duration",
        unit: "s",
        description: "Duration of consumed message processing, in seconds.");

    /// <summary>
    /// Processes a single consumed message, dispatches it through the pipeline, and applies the resulting consumer action.
    /// </summary>
    /// <param name="provider">The service provider used to create a scoped message-processing pipeline.</param>
    /// <param name="logger">The logger used to record failures while applying the error action.</param>
    /// <param name="consumer">The consumer responsible for settling the message.</param>
    /// <param name="subscription">The subscription associated with the consumed message.</param>
    /// <param name="message">The consumed message to process.</param>
    /// <param name="metricTags">The metric tags recorded for processing telemetry.</param>
    /// <param name="cancellationToken">A token that cancels the message-processing operation.</param>
    protected async ValueTask ProcessAsync(
        IServiceProvider provider,
        ILogger logger,
        IConsumer consumer,
        ISubscription subscription,
        Message message,
        KeyValuePair<string, object?>[] metricTags,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (subscription.MessageProcessingTimeout > TimeSpan.Zero)
        {
            cts.CancelAfter(subscription.MessageProcessingTimeout);
        }
        
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
            $"{subscription.Name} message process",
            ActivityKind.Consumer,
            parentContext,
            tags: spanTags);

        var duration = Stopwatch.StartNew();

        await using var scope = provider.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        try
        {
            var response = await dispatcher.QueryAsync<object?>(message, new AmanhecerContext
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
                    TelemetryTags = [.. metricTags],
                }, cts.Token)
                .ConfigureAwait(subscription.ContinueOnCapturedContext);

            if (response is IResolvingConsumerAction resolver)
            {
                response = await resolver.ExecuteAsync(message, subscription, dispatcher, cts.Token)
                    .ConfigureAwait(subscription.ContinueOnCapturedContext);
            }

            var action = response as IConsumerAction ?? Ack.Instance;

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

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Error,
            "The error action of subscription {SubscriptionName} failed; the message is nacked instead.")]
        public static partial void ErrorActionFailed(ILogger logger, string subscriptionName, Exception exception);
    }
}
