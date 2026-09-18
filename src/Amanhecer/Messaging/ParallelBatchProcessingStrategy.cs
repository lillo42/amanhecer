using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging;

/// <summary>
/// Processes batches of messages in parallel, optionally preserving sequential processing within each partition.
/// </summary>
public class ParallelBatchProcessingStrategy : BatchProcessingStrategyBase, IBatchProcessingStrategy
{
    /// <summary>
    /// Gets or sets the options that control parallel execution for batch processing.
    /// </summary>
    public ParallelOptions ParallelExecutionOptions { get; set; } = new ParallelOptions();

    /// <summary>
    /// Gets or sets a value indicating whether messages that share the same partition should be processed sequentially.
    /// </summary>
    public bool ProcessPartitionsSequentially { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether messages with a <see langword="null"/> partition key should be grouped together.
    /// </summary>
    public bool GroupNullPartitionKeysTogether { get; set; }

    /// <summary>
    /// Processes the supplied batch of messages using the configured parallel strategy.
    /// </summary>
    /// <param name="provider">The service provider used to resolve processing dependencies.</param>
    /// <param name="subscription">The subscription associated with the consumed messages.</param>
    /// <param name="consumer">The consumer used to settle each processed message.</param>
    /// <param name="messages">The batch of messages to process.</param>
    /// <param name="cancellationToken">A token that cancels the batch processing operation.</param>
    public async ValueTask ExecuteAsync(IServiceProvider provider,
        ISubscription subscription,
        IConsumer consumer,
        Message[] messages,
        CancellationToken cancellationToken)
    {
        if (!ProcessPartitionsSequentially)
        {
            await FullParallelExecuteAsync(provider, subscription, consumer, messages, cancellationToken);
            return;
        }

        var groups = messages
            .GroupBy(x =>
            {
                if (GroupNullPartitionKeysTogether)
                {
                    return x.PartitionKey;
                }

                return x.PartitionKey ?? Uuid.NewGuid().ToString();
            }, x => x)
            .ToList();

        if (groups.Count == messages.Length)
        {
            await FullParallelExecuteAsync(provider, subscription, consumer, messages, cancellationToken);
        }
        else
        {
            var groupedMessages = groups.Select(x => x.ToArray());
            await ParallelExecuteAsync(provider, subscription, consumer, groupedMessages, cancellationToken);
        }
    }


    private async Task FullParallelExecuteAsync(IServiceProvider provider,
        ISubscription subscription,
        IConsumer consumer,
        Message[] messages,
        CancellationToken cancellationToken)
    {
        var logger = provider.GetRequiredService<ILogger<ParallelBatchProcessingStrategy>>();
        var metrics = new KeyValuePair<string, object?>[]
        {
            new("messaging.system", subscription.MessagingSystem),
            new("messaging.operation.type", "process"),
            new("messaging.destination.name", subscription.Name),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (subscription.BatchProcessingTimeout > TimeSpan.Zero)
        {
            cts.CancelAfter(subscription.BatchProcessingTimeout);
        }

#if NET8_0_OR_GREATER
        await Parallel.ForEachAsync(messages, new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = ParallelExecutionOptions.MaxDegreeOfParallelism,
                TaskScheduler = ParallelExecutionOptions.TaskScheduler,
            },
            async (message, token) =>
            {
                await ProcessAsync(provider, logger, consumer, subscription, message, metrics, token);
            });
#else
        var tasks = new List<Task>(ParallelExecutionOptions.MaxDegreeOfParallelism);
        foreach (var message in messages)
        {
            tasks.Add(Action(message, cts.Token));

            if (tasks.Count == ParallelExecutionOptions.MaxDegreeOfParallelism)
            {
                await Task.WhenAny(tasks);
                tasks.RemoveAll(x => x.Status == TaskStatus.RanToCompletion);
            }
        }

        await Task.WhenAll(tasks);
        
        async Task Action(Message message, CancellationToken ct)
        {
            await ProcessAsync(provider, logger, consumer, subscription, message, metrics, ct);
        }
#endif
    }

    private async Task ParallelExecuteAsync(IServiceProvider provider,
        ISubscription subscription,
        IConsumer consumer,
        IEnumerable<Message[]> groupedMessages,
        CancellationToken cancellationToken)
    {
        var logger = provider.GetRequiredService<ILogger<ParallelBatchProcessingStrategy>>();
        var metrics = new KeyValuePair<string, object?>[]
        {
            new("messaging.system", subscription.MessagingSystem),
            new("messaging.operation.type", "process"),
            new("messaging.destination.name", subscription.Name),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (subscription.BatchProcessingTimeout > TimeSpan.Zero)
        {
            cts.CancelAfter(subscription.BatchProcessingTimeout);
        }

#if NET8_0_OR_GREATER
        await Parallel.ForEachAsync(groupedMessages, new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = ParallelExecutionOptions.MaxDegreeOfParallelism,
                TaskScheduler = ParallelExecutionOptions.TaskScheduler,
            },
            async (messages, token) =>
            {
                foreach (var message in messages)
                {
                    await ProcessAsync(provider,
                        logger,
                        consumer,
                        subscription,
                        message,
                        metrics,
                        token);
                }
            });
#else
        var tasks = new List<Task>(ParallelExecutionOptions.MaxDegreeOfParallelism);
        foreach (var message in groupedMessages)
        {
            tasks.Add(Action(message, cancellationToken));

            if (tasks.Count == ParallelExecutionOptions.MaxDegreeOfParallelism)
            {
                await Task.WhenAny(tasks);
                tasks.RemoveAll(x => x.Status == TaskStatus.RanToCompletion);
            }
        }

        await Task.WhenAll(tasks);
        
        async Task Action(Message[] messages, CancellationToken ct)
        {
            foreach (var message in messages)
            {
                await ProcessAsync(provider, logger, consumer, subscription, message, metrics, ct);
            }
        }
#endif
    }
}
