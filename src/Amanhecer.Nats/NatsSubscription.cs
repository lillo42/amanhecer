using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Nats;

public class NatsSubscription(string toRoutingKey) : Subscription(toRoutingKey)
{
    
}