using System;

namespace Amanhecer.Abstractions.Messaging;

public interface ISubscription
{
    string DefaultSpecVersion { get; }
    Uri DefaultSource { get; }
    string DefaultType { get; }
    
    string RoutingKey { get; }
    
    ISubscriptionProvisoner? Provisioner { get; }
}