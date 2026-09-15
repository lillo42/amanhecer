using System;
using Amanhecer.Dekaf.Provisioners;

namespace Amanhecer.Dekaf.Configurations;

/// <summary>
/// Configures how a topic is created on the cluster when it does not already exist.
/// </summary>
public class CreateTopicConfigurator
{
    private readonly CreateTopic _provisioner = new();

    /// <summary>
    /// Sets the number of partitions of the topic. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="numPartitions">The number of partitions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator NumPartitions(int numPartitions)
    {
        if (numPartitions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numPartitions),
                "The number of partitions must be greater than zero.");
        }

        _provisioner.NumPartitions = numPartitions;
        return this;
    }

    /// <summary>
    /// Sets the replication factor of the topic. Defaults to <c>1</c>.
    /// </summary>
    /// <param name="replicationFactor">The replication factor.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public CreateTopicConfigurator ReplicationFactor(short replicationFactor)
    {
        if (replicationFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replicationFactor),
                "The replication factor must be greater than zero.");
        }

        _provisioner.ReplicationFactor = replicationFactor;
        return this;
    }

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
        return _provisioner;
    }
}
