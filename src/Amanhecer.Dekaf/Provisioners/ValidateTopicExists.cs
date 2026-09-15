using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Dekaf.Provisioners;

/// <summary>
/// A provisioner that validates the topic of a publication or subscription exists on the
/// cluster, failing if it does not.
/// </summary>
public class ValidateTopicExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not DekafPublication dekafPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(DekafPublication)}.",
                nameof(publication));
        }

        return ValidateAsync(gateway, dekafPublication.Topic);
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

        return ValidateAsync(gateway, dekafSubscription.Topic);
    }

    private static async Task ValidateAsync(IGateway gateway, string topic)
    {
        if (gateway is not DekafGateway dekafGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(DekafGateway)}.",
                nameof(gateway));
        }

        var adminClient = dekafGateway.GetOrCreateAdminClient();
        var topics = await adminClient.ListTopicsAsync();

        if (topics.All(t => t.Name != topic))
        {
            throw new InvalidOperationException($"The topic '{topic}' does not exist on the cluster.");
        }
    }
}
