using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Confluent.Kafka;

namespace Amanhecer.ConfluentKafka.Provisioners;

/// <summary>
/// A provisioner that validates the topic of a publication or subscription exists on the
/// cluster, failing if it does not.
/// </summary>
public class ValidateTopicExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <summary>
    /// Gets or sets the time to wait for the cluster metadata.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not ConfluentKafkaPublication kafkaPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(ConfluentKafkaPublication)}.",
                nameof(publication));
        }

        Validate(gateway, kafkaPublication.Topic);
        return Task.CompletedTask;
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

        Validate(gateway, kafkaSubscription.Topic);
        return Task.CompletedTask;
    }

    private void Validate(IGateway gateway, string topic)
    {
        if (gateway is not ConfluentKafkaGateway kafkaGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(ConfluentKafkaGateway)}.",
                nameof(gateway));
        }

        var adminClient = kafkaGateway.GetOrCreateAdminClient();

        // Requesting the metadata of a named topic makes a broker with auto.create.topics.enable
        // (the Kafka default) create it, which is exactly what this provisioner must not do, so
        // the topic is looked up in the full cluster metadata instead.
        var metadata = adminClient.GetMetadata(Timeout);

        if (metadata.Topics.All(t => t.Topic != topic
                || t.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
            throw new InvalidOperationException($"The topic '{topic}' does not exist on the cluster.");
        }
    }
}
