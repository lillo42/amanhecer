using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uuid = Amanhecer.Abstractions.Uuid;

namespace Amanhecer.ConfluentKafka;

/// <summary>
/// An <see cref="IConsumer"/> that consumes records from a Kafka topic and settles them
/// (ack, nack or defer) once they have been processed.
/// </summary>
/// <remarks>
/// Kafka has no per-message settlement: acknowledging a message <em>stores</em> the offset
/// following its record, and stored offsets are <em>committed</em> when the batch reaches
/// <see cref="ConfluentKafkaSubscription.CommitBatchSize"/>, or by a sweeper every
/// <see cref="ConfluentKafkaSubscription.SweepUncommittedOffsetsInterval"/>, whichever comes
/// first — the sweeper prevents low-traffic topics holding uncommitted offsets for long
/// periods. Remaining offsets are committed when the consumer is disposed and when
/// partitions are revoked during a rebalance. Negatively acknowledging a message stores its
/// offset the same way, so the record is not consumed again (Kafka has no dead-letter
/// destination of its own: the message is dropped by this consumer group). Deferring a
/// message seeks back to its record so it is consumed again, pausing the partition for the
/// requested delay. Because committing or seeking an offset settles the whole partition
/// prefix, settling messages out of order within a partition also settles the records
/// between them.
/// </remarks>
public partial class ConfluentKafkaConsumer : IConsumer, IDisposable
{
    // The Kafka revoke window is ~10s; use half to leave time for cleanup.
    private static readonly TimeSpan s_commitSyncTimeout = TimeSpan.FromSeconds(5);

    private readonly IConsumer<string?, byte[]> _consumer;
    private readonly ConfluentKafkaSubscription _subscription;
    private readonly ILogger<ConfluentKafkaConsumer> _logger;
    private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = [];
    private readonly SemaphoreSlim _flushToken = new(1, 1);
    private readonly Timer _sweeperTimer;
    private DateTime _lastFlushAt = DateTime.UtcNow;
    private bool _hasFatalError;
    private bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfluentKafkaConsumer"/> class: builds
    /// the consumer client from <paramref name="config"/>, subscribes it to the
    /// subscription's topic and starts the offset sweeper.
    /// </summary>
    /// <param name="config">The consumer configuration; the consumer applies its partition
    /// and error handlers when building the client.</param>
    /// <param name="subscription">The subscription this consumer consumes for.</param>
    /// <param name="logger">The logger used to report consumption and settlement problems.</param>
    public ConfluentKafkaConsumer(ConsumerConfig config,
        ConfluentKafkaSubscription subscription,
        ILogger<ConfluentKafkaConsumer>? logger = null)
    {
        _subscription = subscription;
        Subscription = subscription;
        _logger = logger ?? NullLogger<ConfluentKafkaConsumer>.Instance;

        _consumer = new ConsumerBuilder<string?, byte[]>(config)
            .SetPartitionsRevokedHandler((_, revoked) =>
            {
                // Commit the offsets stored for the revoked partitions before they are
                // assigned elsewhere, so they are not consumed again by the group.
                CommitOffsetsFor(revoked);
            })
            .SetPartitionsLostHandler((_, lost) =>
            {
                Logger.PartitionsLost(_logger,
                    string.Join(",", lost.Select(tpo => $"{tpo.Topic} : {tpo.Partition}")));
            })
            .SetErrorHandler((_, error) => HandleError(error))
            .Build();

        _consumer.Subscribe(subscription.Topic);

        _sweeperTimer = CreateSweeperTimer();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfluentKafkaConsumer"/> class over an
    /// already-built consumer client. Used by tests to inject a client double.
    /// </summary>
    /// <param name="consumer">The consumer client.</param>
    /// <param name="subscription">The subscription this consumer consumes for.</param>
    /// <param name="logger">The logger used to report consumption and settlement problems.</param>
    internal ConfluentKafkaConsumer(IConsumer<string?, byte[]> consumer,
        ConfluentKafkaSubscription subscription,
        ILogger<ConfluentKafkaConsumer>? logger = null)
    {
        _subscription = subscription;
        Subscription = subscription;
        _logger = logger ?? NullLogger<ConfluentKafkaConsumer>.Instance;
        _consumer = consumer;

        _sweeperTimer = CreateSweeperTimer();
    }

    private Timer CreateSweeperTimer()
    {
        return new Timer(_ => SweepOffsets(),
            null,
            _subscription.SweepUncommittedOffsetsInterval,
            _subscription.SweepUncommittedOffsetsInterval);
    }

    /// <inheritdoc />
    public ISubscription Subscription { get; }

    /// <inheritdoc />
    public ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (_hasFatalError)
        {
            throw new InvalidOperationException(
                "Fatal error on the Kafka consumer; see logs for details.");
        }

        var result = _consumer.Consume(cancellationToken);
        return new ValueTask<Message[]>([ToMessage(result)]);
    }

    /// <summary>
    /// Acknowledges the message by storing the offset following its record. Stored offsets
    /// are committed when the batch reaches
    /// <see cref="ConfluentKafkaSubscription.CommitBatchSize"/>, or by the sweeper after
    /// <see cref="ConfluentKafkaSubscription.SweepUncommittedOffsetsInterval"/>, whichever
    /// comes first.
    /// </summary>
    /// <param name="message">The message to acknowledge.</param>
    /// <returns>A completed <see cref="ValueTask"/>: storing the offset does not block.</returns>
    public ValueTask AckAsync(Message message)
    {
        if (TryGetTopicPartitionOffset(message, out var topicPartitionOffset))
        {
            StoreOffset(topicPartitionOffset);
        }

        return new ValueTask();
    }

    /// <summary>
    /// Negatively acknowledges the message by storing the offset following its record, so it
    /// is not consumed again. Kafka has no dead-letter destination of its own: the message
    /// is effectively dropped by this consumer group.
    /// </summary>
    /// <param name="message">The message to negatively acknowledge.</param>
    /// <returns>A completed <see cref="ValueTask"/>: storing the offset does not block.</returns>
    public ValueTask NackAsync(Message message)
    {
        if (TryGetTopicPartitionOffset(message, out var topicPartitionOffset))
        {
            StoreOffset(topicPartitionOffset);
        }

        return new ValueTask();
    }

    /// <summary>
    /// Defers the message by seeking back to its record, so it is consumed again. When
    /// <paramref name="delay"/> is positive, the partition is paused for the delay before
    /// consumption resumes. Note that seeking also redelivers the records after the deferred
    /// one in the same partition.
    /// </summary>
    /// <param name="message">The message to defer.</param>
    /// <param name="delay">The delay after which consumption of the partition resumes.</param>
    /// <returns>A completed <see cref="ValueTask"/>: seeking does not block.</returns>
    public ValueTask DeferAsync(Message message, TimeSpan delay)
    {
        if (!TryGetTopicPartitionOffset(message, out var topicPartitionOffset))
        {
            return new ValueTask();
        }

        try
        {
            Logger.DeferringMessage(_logger,
                topicPartitionOffset.Offset.Value,
                topicPartitionOffset.Topic,
                topicPartitionOffset.Partition.Value);

            _consumer.Seek(topicPartitionOffset);

            if (delay > TimeSpan.Zero)
            {
                _consumer.Pause([topicPartitionOffset.TopicPartition]);
                _ = ResumeAfterDelay(topicPartitionOffset.TopicPartition, delay);
            }
        }
        catch (Exception e) when (e is KafkaException or InvalidOperationException)
        {
            Logger.ErrorSeekingOffsetForDefer(_logger, e.Message);
        }

        return new ValueTask();
    }

    /// <summary>
    /// Handles an error raised by the underlying Kafka consumer. Fatal errors latch: once
    /// librdkafka reports one, the consumer is unrecoverable and
    /// <see cref="GetMessagesAsync"/> throws from then on.
    /// </summary>
    internal void HandleError(Error error)
    {
        // Errors arrive in bursts: never clear the latch when a non-fatal error follows a
        // fatal one.
        if (error.IsFatal)
        {
            _hasFatalError = true;
        }

        Logger.KafkaError(_logger,
            error.IsFatal ? LogLevel.Error : LogLevel.Warning,
            error.Code,
            error.Reason,
            error.IsFatal);
    }

    private void StoreOffset(TopicPartitionOffset topicPartitionOffset)
    {
        _offsetStorage.Add(new TopicPartitionOffset(topicPartitionOffset.TopicPartition,
            topicPartitionOffset.Offset + 1));

        if (_offsetStorage.Count % _subscription.CommitBatchSize == 0)
        {
            FlushOffsets();
        }
    }

    // The batch size has been reached: commit on a background thread, unless another commit
    // is already in flight (the next batch or sweep commits these offsets instead).
    private void FlushOffsets()
    {
        if (_flushToken.Wait(TimeSpan.Zero))
        {
            Task.Factory.StartNew(
                _ => CommitOffsets(),
                null,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }
        else
        {
            Logger.SkippedCommittingOffsets(_logger);
        }
    }

    // Commits up to one batch's worth of stored offsets, so a busy pump keeps triggering
    // batches instead of one commit draining the bag indefinitely.
    private void CommitOffsets()
    {
        try
        {
            var offsets = new List<TopicPartitionOffset>();
            for (var i = 0; i < _subscription.CommitBatchSize; i++)
            {
                if (_offsetStorage.TryTake(out var offset))
                {
                    offsets.Add(offset);
                }
                else
                {
                    break;
                }
            }

            if (offsets.Count > 0)
            {
                _consumer.Commit(offsets);
            }
        }
        catch (Exception e)
        {
            Logger.ErrorCommittingOffsets(_logger, e.Message);
        }
        finally
        {
            _flushToken.Release(1);
        }
    }

    // If it has been too long since the last flush, commit everything stored, so partially
    // complete batches on low-traffic topics do not linger uncommitted.
    private void SweepOffsets()
    {
        if (_isClosed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastFlushAt < _subscription.SweepUncommittedOffsetsInterval)
        {
            return;
        }

        if (!_flushToken.Wait(TimeSpan.Zero))
        {
            Logger.SkippedSweepingOffsets(_logger);
            return;
        }

        Task.Factory.StartNew(
            _ => CommitAllOffsets(now),
            null,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private void CommitAllOffsets(DateTime flushTime)
    {
        try
        {
            var offsets = new List<TopicPartitionOffset>();
            while (_offsetStorage.TryTake(out var offset))
            {
                offsets.Add(offset);
            }

            if (offsets.Count > 0)
            {
                _consumer.Commit(offsets);
            }

            _lastFlushAt = flushTime;
        }
        catch (Exception e)
        {
            Logger.ErrorCommittingOffsets(_logger, e.Message);
        }
        finally
        {
            _flushToken.Release(1);
        }
    }

    // Called during a rebalance: commit the stored offsets of the revoked partitions before
    // they are assigned to another member of the group.
    internal void CommitOffsetsFor(List<TopicPartitionOffset> revokedPartitions)
    {
        try
        {
            // librdkafka is not thread-safe for concurrent commits: wait for any in-flight
            // background commit to finish first.
            if (!_flushToken.Wait(s_commitSyncTimeout))
            {
                Logger.SkippedCommittingOffsetsForRevokedPartitions(_logger);
                return;
            }

            try
            {
                var revokedOffsets = _offsetStorage
                    .Where(tpo => revokedPartitions.Any(r =>
                        r.TopicPartition == tpo.TopicPartition
                        && r.Offset.Value != Offset.Unset.Value
                        && tpo.Offset.Value > r.Offset.Value))
                    .ToList();

                if (revokedOffsets.Count > 0)
                {
                    _consumer.Commit(revokedOffsets);
                }
            }
            finally
            {
                _flushToken.Release(1);
            }
        }
        catch (KafkaException e)
        {
            Logger.ErrorCommittingOffsetsDuringPartitionRevoke(_logger, e.Message);
        }
    }

    private async Task ResumeAfterDelay(TopicPartition topicPartition, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            _consumer.Resume([topicPartition]);
        }
        catch (Exception e) when (e is KafkaException or InvalidOperationException or TaskCanceledException)
        {
            Logger.ErrorResumingPartition(_logger, e.Message);
        }
    }

    /// <summary>
    /// Commits any outstanding offsets, surrenders the partition assignments and releases
    /// the consumer.
    /// </summary>
    public void Dispose()
    {
        _sweeperTimer.Dispose();

        if (!_isClosed)
        {
            try
            {
                // Wait for any in-flight background commit before committing what remains.
                if (_flushToken.Wait(s_commitSyncTimeout))
                {
                    // Releases the flush token.
                    CommitAllOffsets(DateTime.UtcNow);
                }
                else
                {
                    Logger.SkippedCommittingOffsetsBeforeClose(_logger);
                }
            }
            catch (Exception e)
            {
                // Close anyway: the offsets are replayed on the next start.
                Logger.ErrorCommittingOffsetBeforeClosing(_logger, e.Message);
            }
            finally
            {
                _consumer.Close();
                _isClosed = true;
            }
        }

        _consumer.Dispose();
        _flushToken.Dispose();
    }

    private bool TryGetTopicPartitionOffset(Message message, out TopicPartitionOffset topicPartitionOffset)
    {
        if (message.Metadata.TryGetValue(MetadataName.TopicPartitionOffset, out var obj)
            && obj is TopicPartitionOffset tpo)
        {
            topicPartitionOffset = tpo;
            return true;
        }

        topicPartitionOffset = null!;
        Logger.MissingTopicPartitionOffset(_logger, message.Id, _subscription.Topic);
        return false;
    }

    private Message ToMessage(ConsumeResult<string?, byte[]> result)
    {
        var headers = new Dictionary<string, object?>();
        foreach (var header in result.Message.Headers)
        {
            headers[header.Key] = header.GetValueBytes();
        }

        var metadata = new Dictionary<string, object?>
        {
            [MetadataName.TopicPartitionOffset] = result.TopicPartitionOffset,
            [MetadataName.Topic] = result.Topic,
            [MetadataName.Partition] = result.Partition.Value,
            [MetadataName.Offset] = result.Offset.Value
        };

        return new Message
        {
            Id = GetHeaderValue(headers, "ce_id") ?? Uuid.NewGuid().ToString(),
            ContentType = GetContentType(GetHeaderValue(headers, "ce_datacontenttype")),
            CorrelationId = GetHeaderValue(headers, "ce_correlationid") ?? Uuid.NewGuid().ToString(),
            DataRef = GetHeaderValue(headers, "ce_dataref"),
            DataSchema = GetDataSchema(headers),
            Headers = headers,
            Metadata = metadata,
            PartitionKey = result.Message.Key,
            Payload = result.Message.Value,
            ReplyTo = GetHeaderValue(headers, "ce_replyto"),
            Subject = GetHeaderValue(headers, "ce_subject"),
            SpecVersion = GetHeaderValue(headers, "ce_specversion") ?? _subscription.DefaultSpecVersion,
            Source = GetSource(headers),
            Time = GetTime(result.Message.Timestamp, headers),
            Type = GetHeaderValue(headers, "ce_type") ?? _subscription.DefaultType,
            Baggage = GetBaggage(headers),
            TraceParent = GetHeaderValue(headers, "ce_traceparent"),
            TraceState = GetTraceState(headers)
        };
    }

    // Kafka header values are raw bytes; the producer encodes CloudEvents attributes with the
    // publication encoding (UTF-8 by default), so they are decoded as UTF-8 here.
    private static string? GetHeaderValue(IDictionary<string, object?> headers, string key)
    {
        if (!headers.TryGetValue(key, out var obj))
        {
            return null;
        }

        return obj switch
        {
            string val => val,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => null
        };
    }

    private static ContentType GetContentType(string? contentType)
    {
        return string.IsNullOrEmpty(contentType)
            ? new ContentType("text/plain")
            : new ContentType(contentType);
    }

    private static Uri? GetDataSchema(Dictionary<string, object?> headers)
    {
        var val = GetHeaderValue(headers, "ce_dataschema");
        return val != null && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : null;
    }

    private Uri GetSource(Dictionary<string, object?> headers)
    {
        var val = GetHeaderValue(headers, "ce_source");
        return val != null && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : _subscription.DefaultSource;
    }

    private static DateTimeOffset GetTime(Timestamp timestamp, Dictionary<string, object?> headers)
    {
        var val = GetHeaderValue(headers, "ce_time");
        if (val != null && DateTimeOffset.TryParse(val,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var time))
        {
            return time;
        }

        // The producer always stamps a record timestamp; fall back to it when the CloudEvents
        // time attribute is missing.
        return timestamp.Type == TimestampType.NotAvailable
            ? DateTimeOffset.UtcNow
            : timestamp.UtcDateTime;
    }

    private static Baggage? GetBaggage(Dictionary<string, object?> headers)
    {
        var val = GetHeaderValue(headers, "ce_baggage");
        return val != null ? Baggage.FromString(val) : null;
    }

    private static TraceState? GetTraceState(Dictionary<string, object?> headers)
    {
        var val = GetHeaderValue(headers, "ce_tracestate");
        return val != null ? TraceState.FromString(val) : null;
    }

    private static partial class Logger
    {
        public static void KafkaError(ILogger logger, LogLevel logLevel, ErrorCode errorCode, string reason, bool isFatal)
        {
            logger.Log(logLevel, "Kafka consumer error. Code: {ErrorCode}, Reason: {Reason}, Fatal: {IsFatal}", errorCode, reason, isFatal);
        }

        [LoggerMessage(LogLevel.Information, "Partitions for consumer lost {Partitions}")]
        public static partial void PartitionsLost(ILogger logger, string partitions);

        [LoggerMessage(LogLevel.Information, "Deferring message at offset {Offset} on topic {Topic} partition {Partition}: seeking back for redelivery")]
        public static partial void DeferringMessage(ILogger logger, long offset, string topic, int partition);

        [LoggerMessage(LogLevel.Warning, "Error seeking offset for defer: {ErrorMessage}")]
        public static partial void ErrorSeekingOffsetForDefer(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Warning, "Error resuming partition after the defer delay: {ErrorMessage}")]
        public static partial void ErrorResumingPartition(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Warning, "Cannot settle message {MessageId} from topic {TopicName}: no topic/partition/offset found in the message metadata")]
        public static partial void MissingTopicPartitionOffset(ILogger logger, string messageId, string topicName);

        [LoggerMessage(LogLevel.Warning, "Error committing offsets: {ErrorMessage}")]
        public static partial void ErrorCommittingOffsets(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Debug, "Skipped committing offsets, as another commit or sweep was running")]
        public static partial void SkippedCommittingOffsets(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Skipped sweeping offsets, as another commit or sweep was running")]
        public static partial void SkippedSweepingOffsets(ILogger logger);

        [LoggerMessage(LogLevel.Warning, "Skipped committing offsets for revoked partitions, timed out waiting for the in-flight commit to complete")]
        public static partial void SkippedCommittingOffsetsForRevokedPartitions(ILogger logger);

        [LoggerMessage(LogLevel.Warning, "Skipped committing offsets before close, timed out waiting for the in-flight commit to complete")]
        public static partial void SkippedCommittingOffsetsBeforeClose(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Error committing offsets before closing: {ErrorMessage}")]
        public static partial void ErrorCommittingOffsetBeforeClosing(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Error, "Error committing offsets during partition revoke: {ErrorMessage}")]
        public static partial void ErrorCommittingOffsetsDuringPartitionRevoke(ILogger logger, string errorMessage);
    }
}
