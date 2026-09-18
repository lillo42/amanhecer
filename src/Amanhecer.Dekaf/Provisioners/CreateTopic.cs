using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Dekaf.Admin;
using Dekaf.Errors;

namespace Amanhecer.Dekaf.Provisioners;

/// <summary>
/// A provisioner that creates the topic of a publication or subscription if it does not
/// exist yet. A topic that already exists is left untouched.
/// </summary>
public class CreateTopic : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <summary>
    /// Gets or sets the number of partitions of the topic. Defaults to <c>1</c>.
    /// </summary>
    public int NumPartitions { get; set; } = 1;

    /// <summary>
    /// Gets or sets the replication factor of the topic. Defaults to <c>1</c>.
    /// </summary>
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// Gets or sets the topic configuration entries applied at creation.
    /// </summary>
    public Dictionary<string, string> Configs { get; set; } = [];

    /// <summary>
    /// Gets or sets the time to wait for the topic creation to complete on the broker.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not DekafPublication dekafPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(DekafPublication)}.",
                nameof(publication));
        }

        return CreateAsync(gateway, dekafPublication.Topic);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        if (subscription is not DekafSubscription dekafSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(DekafSubscription)}.",
                nameof(subscription));
        }

        return CreateAsync(gateway, dekafSubscription.Topic);
    }

    private async Task CreateAsync(IGateway gateway, string topic)
    {
        if (gateway is not DekafGateway dekafGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(DekafGateway)}.",
                nameof(gateway));
        }

        var adminClient = dekafGateway.GetOrCreateAdminClient();

        try
        {
            await adminClient.CreateTopicsAsync(
                [new NewTopic
                {
                    Name = topic,
                    NumPartitions = NumPartitions,
                    ReplicationFactor = ReplicationFactor,
                    Configs = Configs
                }],
                new CreateTopicsOptions { TimeoutMs = (int)Timeout.TotalMilliseconds });
        }
        catch (KafkaException e) when (e.ErrorCode == global::Dekaf.Protocol.ErrorCode.TopicAlreadyExists)
        {
            // The topic already exists: nothing to do.
        }
    }
}
