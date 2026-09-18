using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Dekaf;
using Dekaf.Consumer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uuid = Amanhecer.Abstractions.Uuid;

namespace Amanhecer.Dekaf;

/// <summary>
/// An <see cref="IConsumer"/> that consumes records from a Kafka topic and settles them
/// (ack, nack or defer) once they have been processed.
/// </summary>
/// <remarks>
/// Kafka has no per-message settlement: acknowledging a message <em>stores</em> the offset
/// following its record, and stored offsets are <em>committed</em> when the batch reaches
/// <see cref="DekafSubscription.CommitBatchSize"/>, or by a sweeper every
/// <see cref="DekafSubscription.SweepUncommittedOffsetsInterval"/>, whichever comes first —
/// the sweeper prevents low-traffic topics holding uncommitted offsets for long periods.
/// Remaining offsets are committed when the consumer is disposed. Negatively acknowledging a
/// message stores its offset the same way, so the record is not consumed again (Kafka has no
/// dead-letter destination of its own: the message is dropped by this consumer group).
/// Deferring a message seeks back to its record so it is consumed again, pausing the
/// partition for the requested delay. Because committing or seeking an offset settles the
/// whole partition prefix, settling messages out of order within a partition also settles
/// the records between them.
/// </remarks>
public partial class DekafConsumer : IConsumer, IAsyncDisposable
{
    // The Kafka revoke window is ~10s; use half to leave time for cleanup.
    private static readonly TimeSpan SCommitSyncTimeout = TimeSpan.FromSeconds(5);

    private readonly IKafkaConsumer<string, byte[]> _consumer;
    private readonly DekafSubscription _subscription;
    private readonly ILogger<DekafConsumer> _logger;
    private readonly object _offsetLock = new();
    private readonly Dictionary<(string Topic, int Partition), long> _pendingOffsets = [];
    private readonly Dictionary<(string Topic, int Partition), long> _committedOffsets = [];
    private readonly SemaphoreSlim _flushToken = new(1, 1);
    private readonly Timer _sweeperTimer;
    private long _acksSinceLastCommit;
    private DateTime _lastFlushAt = DateTime.UtcNow;
    private bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DekafConsumer"/> class over an
    /// already-built consumer client, and starts the offset sweeper.
    /// </summary>
    /// <param name="consumer">The consumer client, already configured with the bootstrap
    /// servers, group id, offset commit mode and topic subscription.</param>
    /// <param name="subscription">The subscription this consumer consumes for.</param>
    /// <param name="logger">The logger used to report consumption and settlement problems.</param>
    public DekafConsumer(IKafkaConsumer<string, byte[]> consumer,
        DekafSubscription subscription,
        ILogger<DekafConsumer>? logger = null)
    {
        _subscription = subscription;
        _logger = logger ?? NullLogger<DekafConsumer>.Instance;
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
    public ISubscription Subscription => _subscription;

    /// <inheritdoc />
    public async ValueTask<Message[]> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        await _consumer.InitializeAsync(cancellationToken);
        await foreach (var batch in _consumer.ConsumeBatchAsync(cancellationToken))
        {
            if (batch.IsPartitionEof)
            {
                break;
            }

            return [.. batch.Select(ToMessage)];
        }

        return [];
    }

    /// <summary>
    /// Acknowledges the message by storing the offset following its record. Stored offsets
    /// are committed when the batch reaches <see cref="DekafSubscription.CommitBatchSize"/>,
    /// or by the sweeper after
    /// <see cref="DekafSubscription.SweepUncommittedOffsetsInterval"/>, whichever comes
    /// first.
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
                topicPartitionOffset.Offset,
                topicPartitionOffset.Topic,
                topicPartitionOffset.Partition);

            _consumer.Positions.Seek(topicPartitionOffset);

            if (delay > TimeSpan.Zero)
            {
                var topicPartition = new TopicPartition(topicPartitionOffset.Topic, topicPartitionOffset.Partition);
                _consumer.Partitions.Pause(topicPartition);
                _ = ResumeAfterDelay(topicPartition, delay);
            }
        }
        catch (Exception e) when (e is global::Dekaf.Errors.KafkaException or InvalidOperationException)
        {
            Logger.ErrorSeekingOffsetForDefer(_logger, e.Message);
        }

        return new ValueTask();
    }

    // Kafka commits one offset per partition, so only the highest acked offset of a partition
    // is worth keeping: coalescing here keeps a commit to a single entry per partition and
    // stops an out-of-order settlement from committing behind a higher offset.
    private void StoreOffset(TopicPartitionOffset topicPartitionOffset)
    {
        var topicPartition = (topicPartitionOffset.Topic, topicPartitionOffset.Partition);
        var nextOffset = topicPartitionOffset.Offset + 1;
        bool reachedBatchSize;

        lock (_offsetLock)
        {
            if (!_pendingOffsets.TryGetValue(topicPartition, out var pending) || nextOffset > pending)
            {
                _pendingOffsets[topicPartition] = nextOffset;
            }

            // The counter is only reset once a commit actually takes the offsets, so a batch
            // that cannot flush right away flushes on the next ack instead of being skipped.
            _acksSinceLastCommit++;
            reachedBatchSize = _acksSinceLastCommit >= _subscription.CommitBatchSize;
        }

        if (reachedBatchSize)
        {
            FlushOffsets();
        }
    }

    // Takes the partitions whose acked offset is ahead of the offset already committed for
    // them, so a commit neither repeats nor regresses an offset.
    private List<TopicPartitionOffset> TakeUncommittedOffsets()
    {
        lock (_offsetLock)
        {
            _acksSinceLastCommit = 0;

            var offsets = new List<TopicPartitionOffset>(_pendingOffsets.Count);
            foreach (var pending in _pendingOffsets)
            {
                if (!_committedOffsets.TryGetValue(pending.Key, out var committed) || pending.Value > committed)
                {
                    offsets.Add(new TopicPartitionOffset(pending.Key.Topic, pending.Key.Partition, pending.Value));
                }
            }

            return offsets;
        }
    }

    private void MarkCommitted(List<TopicPartitionOffset> offsets)
    {
        lock (_offsetLock)
        {
            foreach (var offset in offsets)
            {
                var topicPartition = (offset.Topic, offset.Partition);
                if (!_committedOffsets.TryGetValue(topicPartition, out var committed) || offset.Offset > committed)
                {
                    _committedOffsets[topicPartition] = offset.Offset;
                }
            }
        }
    }

    // The batch size has been reached: commit on a background task, unless another commit is
    // already in flight (the next batch or sweep commits these offsets instead).
    private void FlushOffsets()
    {
        if (_flushToken.Wait(TimeSpan.Zero))
        {
            _ = Task.Run(CommitOffsetsAsync);
        }
        else
        {
            Logger.SkippedCommittingOffsets(_logger);
        }
    }

    // Commits every offset acked since the last commit. A failed commit leaves the pending
    // offsets in place, so the next batch or sweep retries them.
    private async Task CommitOffsetsAsync()
    {
        try
        {
            var offsets = TakeUncommittedOffsets();
            if (offsets.Count > 0)
            {
                await _consumer.CommitAsync(offsets);
                MarkCommitted(offsets);
            }

            _lastFlushAt = DateTime.UtcNow;
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

    // If it has been too long since the last commit, commit everything pending, so partially
    // complete batches on low-traffic topics do not linger uncommitted.
    private void SweepOffsets()
    {
        if (_isClosed)
        {
            return;
        }

        if (DateTime.UtcNow - _lastFlushAt < _subscription.SweepUncommittedOffsetsInterval)
        {
            return;
        }

        if (!_flushToken.Wait(TimeSpan.Zero))
        {
            Logger.SkippedSweepingOffsets(_logger);
            return;
        }

        _ = Task.Run(CommitOffsetsAsync);
    }

    private async Task ResumeAfterDelay(TopicPartition topicPartition, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            _consumer.Partitions.Resume(topicPartition);
        }
        catch (Exception e) when (e is global::Dekaf.Errors.KafkaException or InvalidOperationException
                                      or TaskCanceledException)
        {
            Logger.ErrorResumingPartition(_logger, e.Message);
        }
    }

    /// <summary>
    /// Commits any outstanding offsets, surrenders the partition assignments and releases
    /// the consumer.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the consumer has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
#if NET8_0_OR_GREATER
        await _sweeperTimer.DisposeAsync();
#else
        _sweeperTimer.Dispose();
#endif

        // A background commit holds the flush token while it uses the client, so taking the
        // token is what makes closing the client safe.
        var flushTokenTaken = await _flushToken.WaitAsync(SCommitSyncTimeout);

        if (!_isClosed)
        {
            try
            {
                if (flushTokenTaken)
                {
                    var offsets = TakeUncommittedOffsets();
                    if (offsets.Count > 0)
                    {
                        await _consumer.CommitAsync(offsets);
                    }
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
                if (flushTokenTaken)
                {
                    _flushToken.Release(1);
                }

                _isClosed = true;
            }
        }

        try
        {
            await _consumer.CloseAsync();
        }
        catch
        {
            // Best effort: the consumer may already be closed.
        }

        await _consumer.DisposeAsync();

        // A commit that is still in flight releases the token when it finishes, and releasing
        // a disposed semaphore throws, so the token outlives the consumer in that case.
        if (flushTokenTaken)
        {
            _flushToken.Dispose();
        }
    }

    private bool TryGetTopicPartitionOffset(Message message, out TopicPartitionOffset topicPartitionOffset)
    {
        if (message.Metadata.TryGetValue(MetadataName.TopicPartitionOffset, out var obj) &&
            obj is TopicPartitionOffset tpo)
        {
            topicPartitionOffset = tpo;
            return true;
        }

        topicPartitionOffset = default;
        Logger.MissingTopicPartitionOffset(_logger, message.Id, _subscription.Topic);
        return false;
    }

    private Message ToMessage(ConsumeResult<string, byte[]> result)
    {
        var headers = new Dictionary<string, object?>();
        foreach (var header in result.Headers)
        {
            headers[header.Key] = header.IsValueNull ? null : header.Value.ToArray();
        }

        var metadata = new Dictionary<string, object?>
        {
            [MetadataName.TopicPartitionOffset] = result.TopicPartitionOffset,
            [MetadataName.Topic] = result.Topic,
            [MetadataName.Partition] = result.Partition,
            [MetadataName.Offset] = result.Offset
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
            PartitionKey = result.Key,
            Payload = result.Value.ToArray(),
            ReplyTo = GetHeaderValue(headers, "ce_replyto"),
            Subject = GetHeaderValue(headers, "ce_subject"),
            SpecVersion = GetHeaderValue(headers, "ce_specversion") ?? _subscription.DefaultSpecVersion,
            Source = GetSource(headers),
            Time = GetTime(result.Timestamp, headers),
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

    private static DateTimeOffset GetTime(DateTimeOffset timestamp, Dictionary<string, object?> headers)
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
        return timestamp == default
            ? DateTimeOffset.UtcNow
            : timestamp;
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
        [LoggerMessage(LogLevel.Information,
            "Deferring message at offset {Offset} on topic {Topic} partition {Partition}: seeking back for redelivery")]
        public static partial void DeferringMessage(ILogger logger, long offset, string topic, int partition);

        [LoggerMessage(LogLevel.Warning, "Error seeking offset for defer: {ErrorMessage}")]
        public static partial void ErrorSeekingOffsetForDefer(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Warning, "Error resuming partition after the defer delay: {ErrorMessage}")]
        public static partial void ErrorResumingPartition(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Warning,
            "Cannot settle message {MessageId} from topic {TopicName}: no topic/partition/offset found in the message metadata")]
        public static partial void MissingTopicPartitionOffset(ILogger logger, string messageId, string topicName);

        [LoggerMessage(LogLevel.Warning, "Error committing offsets: {ErrorMessage}")]
        public static partial void ErrorCommittingOffsets(ILogger logger, string errorMessage);

        [LoggerMessage(LogLevel.Debug, "Skipped committing offsets, as another commit or sweep was running")]
        public static partial void SkippedCommittingOffsets(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Skipped sweeping offsets, as another commit or sweep was running")]
        public static partial void SkippedSweepingOffsets(ILogger logger);

        [LoggerMessage(LogLevel.Warning,
            "Skipped committing offsets before close, timed out waiting for the in-flight commit to complete")]
        public static partial void SkippedCommittingOffsetsBeforeClose(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Error committing offsets before closing: {ErrorMessage}")]
        public static partial void ErrorCommittingOffsetBeforeClosing(ILogger logger, string errorMessage);
    }
}