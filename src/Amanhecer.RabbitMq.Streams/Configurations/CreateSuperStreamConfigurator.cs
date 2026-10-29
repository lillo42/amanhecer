using System;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures how a super stream is declared on the broker when it does not already exist,
/// used by both publication and subscription provisioning.
/// </summary>
public class CreateSuperStreamConfigurator
{
    private TimeSpan? _maxAge;

    /// <summary>
    /// Sets the maximum age of messages in the super stream partitions before they are deleted.
    /// </summary>
    /// <param name="maxAge">The maximum message age.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateSuperStreamConfigurator MaxAge(TimeSpan maxAge)
    {
        if (maxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge), "Max age must be greater than zero.");
        }

        _maxAge = maxAge;
        return this;
    }

    private ulong? _maxLengthBytes;

    /// <summary>
    /// Sets the maximum total size in bytes of each super stream partition before old segments are deleted.
    /// </summary>
    /// <param name="maxLengthBytes">The maximum partition size in bytes.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateSuperStreamConfigurator MaxLengthBytes(ulong maxLengthBytes)
    {
        _maxLengthBytes = maxLengthBytes;
        return this;
    }

    private int? _maxSegmentSizeBytes;

    /// <summary>
    /// Sets the maximum size in bytes of each segment file of the super stream partitions.
    /// </summary>
    /// <param name="maxSegmentSizeBytes">The maximum segment size in bytes.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateSuperStreamConfigurator MaxSegmentSizeBytes(int maxSegmentSizeBytes)
    {
        if (maxSegmentSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSegmentSizeBytes),
                "Max segment size must be greater than zero.");
        }

        _maxSegmentSizeBytes = maxSegmentSizeBytes;
        return this;
    }

    private LeaderLocator? _leaderLocator;

    /// <summary>
    /// Sets the leader-locator strategy used when creating the super stream partitions.
    /// </summary>
    /// <param name="leaderLocator">The leader locator strategy.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateSuperStreamConfigurator LeaderLocator(LeaderLocator leaderLocator)
    {
        _leaderLocator = leaderLocator;
        return this;
    }

    private SuperStreamType? _streamType;

    /// <summary>
    /// Sets the shape of the super stream: a number of partitions through
    /// <see cref="PartitionSuperStream"/> or a set of binding keys through
    /// <see cref="BindingSuperStream"/>. Defaults to a single partition.
    /// </summary>
    /// <param name="streamType">The super stream shape.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateSuperStreamConfigurator StreamType(SuperStreamType streamType)
    {
        _streamType = streamType ?? throw new ArgumentNullException(nameof(streamType));
        return this;
    }

    internal CreateSuperStream ToProvisioner()
    {
        return new CreateSuperStream
        {
            MaxAge = _maxAge,
            MaxLengthBytes = _maxLengthBytes,
            MaxSegmentSizeBytes = _maxSegmentSizeBytes,
            LeaderLocator = _leaderLocator,
            StreamType = _streamType ?? PartitionSuperStream.Instance
        };
    }
}
