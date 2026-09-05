using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Provisions the transport resources (queues, bindings, ...) a subscription needs
/// before messages can be consumed from it.
/// </summary>
public interface ISubscriptionProvisioner
{
   /// <summary>
   /// Provisions the transport resources required by the subscription.
   /// </summary>
   /// <param name="gateway">The gateway the subscription belongs to.</param>
   /// <param name="subscription">The subscription to provision.</param>
   /// <returns>A <see cref="Task"/> that completes when provisioning has finished.</returns>
   Task ExecuteAsync(IGateway gateway, ISubscription subscription);
}