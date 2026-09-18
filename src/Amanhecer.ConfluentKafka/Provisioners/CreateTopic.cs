using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Amanhecer.ConfluentKafka.Provisioners;

/// <summary>
/// A provisioner that creates the topic of a publication or subscription if it does not
/// exist yet. A topic that already exists is left untouched.
/// </summary>
public class CreateTopic : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <summary>
    /// Gets or sets the number of partitions of the topic. Defaults to <c>-1</c>, the broker
    /// default (<c>num.partitions</c>).
    /// </summary>
    public int NumPartitions { get; set; } = -1;

    /// <summary>
    /// Gets or sets the replication factor of the topic. Defaults to <c>-1</c>, the broker
    /// default (<c>default.replication.factor</c>).
    /// </summary>
    public short ReplicationFactor { get; set; } = -1;

    /// <summary>
    /// Gets or sets the topic configuration entries applied at creation.
    /// </summary>
    public Dictionary<string, string> Configs { get; set; } = [];

    /// <summary>
    /// Gets or sets the time to wait for the topic creation to complete on the broker.
    /// Defaults to 30 seconds. Without it the request returns as soon as the controller
    /// accepts it, so a consumer subscribing right after provisioning can still fail with
    /// <see cref="ErrorCode.UnknownTopicOrPart"/>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not ConfluentKafkaPublication kafkaPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(ConfluentKafkaPublication)}.",
                nameof(publication));
        }

        return CreateAsync(gateway, kafkaPublication.Topic);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        if (subscription is not ConfluentKafkaSubscription kafkaSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(ConfluentKafkaSubscription)}.",
                nameof(subscription));
        }

        return CreateAsync(gateway, kafkaSubscription.Topic);
    }

    private async Task CreateAsync(IGateway gateway, string topic)
    {
        if (gateway is not ConfluentKafkaGateway kafkaGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(ConfluentKafkaGateway)}.",
                nameof(gateway));
        }

        var adminClient = kafkaGateway.GetOrCreateAdminClient();

        var specification = new TopicSpecification
        {
            Name = topic,
            NumPartitions = NumPartitions,
            ReplicationFactor = ReplicationFactor,
            Configs = Configs
        };

        try
        {
            await adminClient.CreateTopicsAsync([specification],
                new CreateTopicsOptions { OperationTimeout = Timeout });
        }
        catch (CreateTopicsException e) when (e.Results != null
            && e.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // The topic already exists: nothing to do.
        }
    }
}
