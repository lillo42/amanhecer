using System;
using Amanhecer.ConfluentKafka.Provisioners;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures how a topic is created on the cluster when it does not already exist.
/// </summary>
public class CreateTopicConfigurator
{
    private int _numPartitions = -1;

    /// <summary>
    /// Sets the number of partitions of the topic. Defaults to <c>-1</c>, the broker
    /// default (<c>num.partitions</c>).
    /// </summary>
    /// <param name="numPartitions">The number of partitions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator NumPartitions(int numPartitions)
    {
        if (numPartitions < -1 || numPartitions == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numPartitions),
                "The number of partitions must be -1 (broker default) or greater than zero.");
        }

        _numPartitions = numPartitions;
        return this;
    }

    private short _replicationFactor = -1;

    /// <summary>
    /// Sets the replication factor of the topic. Defaults to <c>-1</c>, the broker default
    /// (<c>default.replication.factor</c>).
    /// </summary>
    /// <param name="replicationFactor">The replication factor.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator ReplicationFactor(short replicationFactor)
    {
        if (replicationFactor < -1 || replicationFactor == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replicationFactor),
                "The replication factor must be -1 (broker default) or greater than zero.");
        }

        _replicationFactor = replicationFactor;
        return this;
    }

    private readonly CreateTopic _provisioner = new();

    /// <summary>
    /// Adds a topic configuration entry applied at creation.
    /// </summary>
    /// <param name="key">The configuration name.</param>
    /// <param name="value">The configuration value.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator Config(string key, string value)
    {
        _provisioner.Configs[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the time to wait for the topic creation to complete on the broker.
    /// </summary>
    /// <param name="timeout">The creation timeout.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator Timeout(TimeSpan timeout)
    {
        _provisioner.Timeout = timeout;
        return this;
    }

    internal CreateTopic ToProvisioner()
    {
        _provisioner.NumPartitions = _numPartitions;
        _provisioner.ReplicationFactor = _replicationFactor;
        return _provisioner;
    }
}
