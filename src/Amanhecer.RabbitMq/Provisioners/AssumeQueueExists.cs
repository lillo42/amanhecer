using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Provisioners;

public class AssumeQueueExists : ISubscriptionProvisoner
{
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        throw new System.NotImplementedException();
    }
}