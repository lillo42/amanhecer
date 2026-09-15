using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Dekaf.Provisioners;

/// <summary>
/// A provisioner that assumes the topic of a publication or subscription already exists and
/// performs no action.
/// </summary>
public class AssumeTopicExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        return Task.CompletedTask;
    }
}
