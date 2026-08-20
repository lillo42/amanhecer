using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface ISubscriptionProvisoner
{
   Task ExecuteAsync(IGateway gateway, ISubscription subscription);
}