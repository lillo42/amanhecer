using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq;

public class RabbitMqSubscription : Subscription 
{
    public required string QueueName { get; set; }
    
    public ISubscriptionProvisoner? Provisoner { get; }
}