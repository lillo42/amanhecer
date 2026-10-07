using System;
using Amanhecer.RabbitMq.Streams.Provisioners;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures how a stream is declared on the broker when it does not already exist,
/// used by both publication and subscription provisioning.
/// </summary>
public class CreateStreamConfigurator
{
    private TimeSpan? _maxAge;

    /// <summary>
    /// Sets the maximum age of messages in the stream before they are deleted.
    /// </summary>
    /// <param name="maxAge">The maximum message age.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateStreamConfigurator MaxAge(TimeSpan maxAge)
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
    /// Sets the maximum total size in bytes of the stream before old segments are deleted.
    /// </summary>
    /// <param name="maxLengthBytes">The maximum stream size in bytes.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateStreamConfigurator MaxLengthBytes(ulong maxLengthBytes)
    {
        _maxLengthBytes = maxLengthBytes;
        return this;
    }

    private int? _maxSegmentSizeBytes;

    /// <summary>
    /// Sets the maximum size in bytes of each stream segment file.
    /// </summary>
    /// <param name="maxSegmentSizeBytes">The maximum segment size in bytes.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateStreamConfigurator MaxSegmentSizeBytes(int maxSegmentSizeBytes)
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
    /// Sets the leader-locator strategy used when creating the stream.
    /// </summary>
    /// <param name="leaderLocator">The leader locator strategy.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateStreamConfigurator LeaderLocator(LeaderLocator leaderLocator)
    {
        _leaderLocator = leaderLocator;
        return this;
    }

    internal CreateStream ToProvisioner()
    {
        return new CreateStream
        {
            MaxAge = _maxAge,
            MaxLengthBytes = _maxLengthBytes,
            MaxSegmentSizeBytes = _maxSegmentSizeBytes,
            LeaderLocator = _leaderLocator
        };
    }
}
